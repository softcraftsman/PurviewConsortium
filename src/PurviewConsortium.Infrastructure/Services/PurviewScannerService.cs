using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Infrastructure.Services;

/// <summary>
/// Scans the Microsoft Purview Unified Catalog for curated Data Products
/// using the new Data Products API (api-version 2025-09-15-preview).
/// 
/// Supports two auth flows:
/// 1. On-Behalf-Of (OBO): When a user triggers the scan, their token is exchanged
///    for a Purview token with the user's permissions (sees all governance domains).
/// 2. Client Credentials: Fallback for automated/timer-triggered scans using the SP token.
/// </summary>
public class PurviewScannerService : IPurviewScannerService
{
    private const string UnifiedCatalogBaseUrl = "https://api.purview-service.microsoft.com";
    private const string DataProductsApiVersion = "2025-09-15-preview";
    private const string PurviewScope = "https://purview.azure.net/.default";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PurviewScannerService> _logger;

    public PurviewScannerService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PurviewScannerService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<DataProductSyncResult>> ScanForShareableDataProductsAsync(
        string purviewAccountName,
        string tenantId,
        string? userAccessToken = null,
        string? consortiumDomainIds = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Scanning Purview Unified Catalog for Data Products (account: {Account}, tenant: {Tenant}, domains: {Domains})",
            purviewAccountName, tenantId, consortiumDomainIds ?? "ALL");

        var results = new List<DataProductSyncResult>();

        // Parse domain filter
        var domainFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(consortiumDomainIds))
        {
            foreach (var id in consortiumDomainIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                domainFilter.Add(id);
            }
        }

        try
        {
            var accessToken = await GetAccessTokenAsync(tenantId, userAccessToken, cancellationToken);
            var httpClient = _httpClientFactory.CreateClient("Purview");

            var dataProducts = await FetchDataProductsFromUnifiedCatalogAsync(
                httpClient, accessToken, cancellationToken);

            foreach (var dp in dataProducts)
            {
                // Filter by governance domain if configured
                if (domainFilter.Count > 0)
                {
                    // Extract domain ID — check "governanceDomain" and "domain",
                    // each may be an object with "id"/"name" or a plain string
                    var dpDomainId = ExtractDomainId(dp, "governanceDomain")
                                 ?? ExtractDomainId(dp, "domain");

                    if (dpDomainId == null || !domainFilter.Contains(dpDomainId))
                    {
                        var dpName = dp.TryGetProperty("name", out var nameVal) ? nameVal.GetString() : "unknown";
                        _logger.LogDebug("Skipping Data Product '{Name}' — domain {Domain} not in consortium filter",
                            dpName, dpDomainId);
                        continue;
                    }
                }

                results.Add(MapDataProductToSyncResult(dp));
            }

            _logger.LogInformation(
                "Found {Count} Data Products in Unified Catalog for account {Account} (filtered from {Total} total)",
                results.Count, purviewAccountName, dataProducts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error scanning Unified Catalog for account {Account} in tenant {Tenant}",
                purviewAccountName, tenantId);
            throw;
        }

        return results;
    }

    private async Task<string> GetAccessTokenAsync(string tenantId, string? userAccessToken, CancellationToken cancellationToken)
    {
        var clientId = _configuration["AzureAd:ClientId"];
        var clientSecret = _configuration["AzureAd:ClientSecret"];

        // If we have a user token, use On-Behalf-Of flow to get a Purview token
        // with the user's permissions (sees all governance domains).
        if (!string.IsNullOrEmpty(userAccessToken))
        {
            _logger.LogInformation("Using On-Behalf-Of (OBO) flow with user token for Purview access");
            try
            {
                var confidentialClient = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                    .Build();

                var oboResult = await confidentialClient
                    .AcquireTokenOnBehalfOf(
                        new[] { PurviewScope },
                        new UserAssertion(userAccessToken))
                    .ExecuteAsync(cancellationToken);

                _logger.LogInformation("OBO token acquired successfully for user");
                return oboResult.AccessToken;
            }
            catch (MsalException ex)
            {
                _logger.LogWarning(ex, "OBO token acquisition failed, falling back to client credentials. Error: {Error}", ex.Message);
                // Fall through to client credentials
            }
        }
        else
        {
            _logger.LogInformation("No user token provided; using client credentials for Purview access");
        }

        // Fallback: client credentials (SP token)
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var tokenResult = await credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { PurviewScope }),
            cancellationToken);

        return tokenResult.Token;
    }

    /// <summary>
    /// Fetches all Data Products from the Unified Catalog API with pagination.
    /// </summary>
    private async Task<List<JsonElement>> FetchDataProductsFromUnifiedCatalogAsync(
        HttpClient httpClient,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var allItems = new List<JsonElement>();
        int skip = 0;
        const int pageSize = 100;
        bool hasMore = true;

        while (hasMore)
        {
            var url = $"{UnifiedCatalogBaseUrl}/datagovernance/catalog/dataProducts" +
                      $"?api-version={DataProductsApiVersion}&top={pageSize}&skip={skip}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            _logger.LogInformation("Fetching Data Products from Unified Catalog (skip={Skip})", skip);

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Unified Catalog API returned {StatusCode}: {Body}",
                    response.StatusCode, errorBody);

                if (allItems.Count == 0)
                {
                    throw new HttpRequestException(
                        $"Unified Catalog Data Products API returned {(int)response.StatusCode} " +
                        $"({response.StatusCode}): {errorBody}");
                }

                break; // Return what we have so far
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("value", out var valueArray))
            {
                int count = 0;
                foreach (var item in valueArray.EnumerateArray())
                {
                    allItems.Add(item.Clone());
                    count++;
                }

                hasMore = count == pageSize; // More pages if we got a full page
                skip += count;
            }
            else
            {
                hasMore = false;
            }
        }

        _logger.LogInformation("Retrieved {Count} total Data Products from Unified Catalog", allItems.Count);
        return allItems;
    }

    /// <summary>
    /// Maps a Unified Catalog Data Product JSON element to a DataProductSyncResult.
    /// The Purview Data Products API may use different property names/shapes across versions:
    ///   - Owner info: "owners" array (primary) or "contacts" array (fallback)
    ///   - Sensitivity label: object {"id","name"} or plain string
    ///   - Domain: "governanceDomain" or "domain", as object {"id","name"} or plain string
    /// </summary>
    private static DataProductSyncResult MapDataProductToSyncResult(JsonElement dp)
    {
        // Extract owner from owners array (primary) or contacts array (fallback)
        string? owner = null;
        string? ownerEmail = null;
        owner = ExtractOwnerFromArray(dp, "owners", out ownerEmail);
        if (string.IsNullOrEmpty(owner))
        {
            owner = ExtractOwnerFromArray(dp, "contacts", out ownerEmail);
        }

        // Extract governance domain name — try "governanceDomain" then "domain",
        // each may be an object {"id":"...","name":"..."} or a plain string
        string? domain = ExtractDomainName(dp, "governanceDomain")
                      ?? ExtractDomainName(dp, "domain");

        // Extract sensitivity label — may be object {"id":"...","name":"..."} or plain string
        string? sensitivityLabel = ExtractStringOrObjectName(dp, "sensitivityLabel");

        // Extract asset count from additionalProperties
        int assetCount = 0;
        if (dp.TryGetProperty("additionalProperties", out var addProps) &&
            addProps.ValueKind == JsonValueKind.Object)
        {
            if (addProps.TryGetProperty("assetCount", out var assetCountVal))
            {
                if (assetCountVal.ValueKind == JsonValueKind.Number)
                    assetCount = assetCountVal.GetInt32();
                else if (assetCountVal.ValueKind == JsonValueKind.String &&
                         int.TryParse(assetCountVal.GetString(), out var parsed))
                    assetCount = parsed;
            }
        }

        // Also check top-level assetCount
        if (assetCount == 0 && dp.TryGetProperty("assetCount", out var topLevelAssetCount))
        {
            if (topLevelAssetCount.ValueKind == JsonValueKind.Number)
                assetCount = topLevelAssetCount.GetInt32();
        }

        // Extract last modified from systemData
        DateTime? lastModified = null;
        if (dp.TryGetProperty("systemData", out var systemData) &&
            systemData.ValueKind == JsonValueKind.Object)
        {
            if (systemData.TryGetProperty("lastModifiedAt", out var modifiedAt) &&
                modifiedAt.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(modifiedAt.GetString(), out var dt))
            {
                lastModified = dt;
            }
        }

        // Check endorsed status — may be bool or object
        bool endorsed = dp.TryGetProperty("endorsed", out var endorsedVal) &&
                        endorsedVal.ValueKind == JsonValueKind.True;

        // Build classifications from type + status for discoverability
        var classifications = new List<string>();
        var dpType = GetStringProperty(dp, "type");
        var dpStatus = GetStringProperty(dp, "status");
        if (!string.IsNullOrEmpty(dpType)) classifications.Add($"Type:{dpType}");
        if (!string.IsNullOrEmpty(dpStatus)) classifications.Add($"Status:{dpStatus}");
        if (endorsed) classifications.Add("Endorsed");

        return new DataProductSyncResult
        {
            PurviewQualifiedName = GetStringProperty(dp, "id") ?? GetStringProperty(dp, "name") ?? "unknown",
            Name = GetStringProperty(dp, "name") ?? "Unnamed",
            Description = GetStringProperty(dp, "description") ?? GetStringProperty(dp, "businessUse"),
            Owner = owner,
            OwnerEmail = ownerEmail,
            SourceSystem = domain,
            SchemaJson = null,
            Classifications = classifications,
            GlossaryTerms = new List<string>(),
            SensitivityLabel = sensitivityLabel,
            PurviewLastModified = lastModified,
            Status = dpStatus,
            DataProductType = dpType,
            GovernanceDomain = domain,
            AssetCount = assetCount,
            BusinessUse = GetStringProperty(dp, "businessUse"),
            Endorsed = endorsed,
            UpdateFrequency = GetStringProperty(dp, "updateFrequency"),
            Documentation = GetStringProperty(dp, "documentation")
        };
    }

    /// <summary>
    /// Extracts owner display name and email from a named array property (e.g. "owners" or "contacts").
    /// Handles both {displayName, mail} and {name, email} property names.
    /// </summary>
    private static string? ExtractOwnerFromArray(JsonElement dp, string arrayPropertyName, out string? email)
    {
        email = null;
        if (dp.TryGetProperty(arrayPropertyName, out var array) &&
            array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                var name = GetStringProperty(item, "displayName", "name");
                var mail = GetStringProperty(item, "mail", "email", "userPrincipalName");
                if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(mail))
                {
                    email = mail;
                    return name ?? mail; // Fall back to email if no display name
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts a domain/governance domain name from a property that may be
    /// an object {"id":"...","name":"..."} or a plain string.
    /// </summary>
    private static string? ExtractDomainName(JsonElement dp, string propertyName)
    {
        if (!dp.TryGetProperty(propertyName, out var domainVal))
            return null;

        if (domainVal.ValueKind == JsonValueKind.Object)
            return GetStringProperty(domainVal, "name", "displayName");

        if (domainVal.ValueKind == JsonValueKind.String)
            return domainVal.GetString();

        return null;
    }

    /// <summary>
    /// Extracts a domain ID for filtering. If the property is an object, returns "id";
    /// if it's a string, returns the string value (assumed to be a domain ID or name).
    /// </summary>
    private static string? ExtractDomainId(JsonElement dp, string propertyName)
    {
        if (!dp.TryGetProperty(propertyName, out var domainVal))
            return null;

        if (domainVal.ValueKind == JsonValueKind.Object)
            return GetStringProperty(domainVal, "id", "name");

        if (domainVal.ValueKind == JsonValueKind.String)
            return domainVal.GetString();

        return null;
    }

    /// <summary>
    /// Extracts a value from a property that may be a plain string or
    /// an object with a "name" property (e.g. sensitivityLabel).
    /// </summary>
    private static string? ExtractStringOrObjectName(JsonElement dp, string propertyName)
    {
        if (!dp.TryGetProperty(propertyName, out var val))
            return null;

        if (val.ValueKind == JsonValueKind.String)
            return val.GetString();

        if (val.ValueKind == JsonValueKind.Object)
            return GetStringProperty(val, "name", "displayName", "labelName");

        return null;
    }

    private static string? GetStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }
        return null;
    }
}
