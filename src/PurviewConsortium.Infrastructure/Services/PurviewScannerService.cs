using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Infrastructure.Services;

/// <summary>
/// Scans the Microsoft Purview Unified Catalog for curated Data Products
/// using the new Data Products API (api-version 2025-09-15-preview).
/// 
/// Uses client credentials (service principal) for authentication.
/// The SP must have appropriate Purview permissions in each institution's tenant.
/// </summary>
public class PurviewScannerService : IPurviewScannerService
{
    private const string UnifiedCatalogBaseUrl = "https://api.purview-service.microsoft.com";
    private const string DataProductsApiVersion = "2025-09-15-preview";
    private const string PurviewScope = "https://purview.azure.net/.default";
    private const string GraphScope = "https://graph.microsoft.com/.default";
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
    private const int MaxDomainsPerPage = 200;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PurviewScannerService> _logger;
    private readonly bool _useDeveloperCredential;

    public PurviewScannerService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PurviewScannerService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;

        // In development, use DefaultAzureCredential (developer's az CLI / Visual Studio identity)
        // which has Purview governance access. In production, use the SP client credentials.
        var env = configuration["ASPNETCORE_ENVIRONMENT"]
               ?? configuration["DOTNET_ENVIRONMENT"]
               ?? "Production";
        _useDeveloperCredential = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase)
            && configuration.GetValue<bool>("UseDeveloperCredentialForPurview", true);
    }

    public async Task<List<DataProductSyncResult>> ScanForShareableDataProductsAsync(
        string purviewAccountName,
        string tenantId,
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
            var accessToken = await GetAccessTokenAsync(tenantId, cancellationToken);
            var httpClient = _httpClientFactory.CreateClient("Purview");

            // Fetch governance domain names once so GUID values can be resolved to readable names
            var domainNames = await FetchGovernanceDomainNamesAsync(httpClient, accessToken, cancellationToken);

            var dataProducts = await FetchDataProductsFromUnifiedCatalogAsync(
                httpClient, accessToken, cancellationToken);

            foreach (var dp in dataProducts)
            {
                var dpName = dp.TryGetProperty("name", out var nameVal) ? nameVal.GetString() : "unknown";

                // Filter by governance domain if configured
                if (domainFilter.Count > 0)
                {
                    // Extract domain ID — check "governanceDomain" and "domain",
                    // each may be an object with "id"/"name" or a plain string
                    var dpDomainId = ExtractDomainId(dp, "governanceDomain")
                                 ?? ExtractDomainId(dp, "domain");

                    _logger.LogInformation(
                        "Data Product '{Name}' has domain={DomainId}, filter requires: [{Filter}]",
                        dpName, dpDomainId ?? "(null)", string.Join(",", domainFilter));

                    if (dpDomainId == null || !domainFilter.Contains(dpDomainId))
                    {
                        _logger.LogDebug("Skipping Data Product '{Name}' — domain {Domain} not in consortium filter",
                            dpName, dpDomainId);
                        continue;
                    }
                }

                results.Add(MapDataProductToSyncResult(dp, domainNames));
            }

            // Resolve owner GUIDs to display names/emails via Microsoft Graph
            await ResolveOwnerIdentitiesAsync(results, tenantId, cancellationToken);

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

    protected virtual async Task<string> GetAccessTokenAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (_useDeveloperCredential)
        {
            _logger.LogInformation(
                "Acquiring Purview token via DefaultAzureCredential (developer identity) for tenant {TenantId}",
                tenantId);

            // Uses az CLI / Visual Studio / environment credentials — the developer's identity
            // which has direct Purview governance access
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                TenantId = tenantId
            });
            var tokenResult = await credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(new[] { PurviewScope }),
                cancellationToken);

            _logger.LogInformation("Developer token acquired successfully (expires: {Expires})", tokenResult.ExpiresOn);
            return tokenResult.Token;
        }

        var clientId = _configuration["AzureAd:ClientId"];
        var clientSecret = _configuration["AzureAd:ClientSecret"];

        _logger.LogInformation(
            "Acquiring Purview token via client credentials for tenant {TenantId} (clientId={ClientId}, scope={Scope})",
            tenantId, clientId, PurviewScope);

        var spCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var spTokenResult = await spCredential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { PurviewScope }),
            cancellationToken);

        _logger.LogInformation("SP token acquired successfully (expires: {Expires})", spTokenResult.ExpiresOn);
        return spTokenResult.Token;
    }

    /// <summary>
    /// Gets a Microsoft Graph access token for the given tenant to resolve user identities.
    /// </summary>
    private async Task<string> GetGraphAccessTokenAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (_useDeveloperCredential)
        {
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = tenantId });
            var tokenResult = await credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(new[] { GraphScope }), cancellationToken);
            return tokenResult.Token;
        }

        var clientId = _configuration["AzureAd:ClientId"];
        var clientSecret = _configuration["AzureAd:ClientSecret"];
        var spCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var spTokenResult = await spCredential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { GraphScope }), cancellationToken);
        return spTokenResult.Token;
    }

    /// <summary>
    /// Fetches all governance domains from Purview and returns a dictionary of id → name.
    /// This is used to resolve domain GUIDs to human-readable names when Purview returns
    /// a plain GUID string in the governanceDomain field.
    /// </summary>
    private async Task<Dictionary<string, string>> FetchGovernanceDomainNamesAsync(
        HttpClient httpClient,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var url = $"{UnifiedCatalogBaseUrl}/datagovernance/catalog/domains" +
                      $"?api-version={DataProductsApiVersion}&top={MaxDomainsPerPage}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Governance domains API returned {StatusCode} — domain GUIDs may not be resolved to names",
                    response.StatusCode);
                return map;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("value", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var id = GetStringProperty(item, "id");
                    var name = GetStringProperty(item, "name", "displayName");
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                        map[id] = name;
                }
            }

            _logger.LogInformation("Fetched {Count} governance domain name(s) for GUID resolution", map.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch governance domain names — domain GUIDs may appear as-is");
        }
        return map;
    }

    /// <summary>
    /// Resolves owner GUIDs to display names and emails via Microsoft Graph API.
    /// Batches unique GUIDs to minimize API calls.
    /// </summary>
    private async Task ResolveOwnerIdentitiesAsync(
        List<DataProductSyncResult> results,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var guidsToResolve = results
            .SelectMany(r => r.OwnerContacts.Select(c => c.Id).Append(r.OwnerObjectId))
            .Where(id => !string.IsNullOrEmpty(id) && Guid.TryParse(id, out _))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (guidsToResolve.Count == 0)
        {
            _logger.LogDebug("No owner GUIDs to resolve via Graph");
            return;
        }

        _logger.LogInformation("Resolving {Count} owner GUID(s) via Microsoft Graph", guidsToResolve.Count);

        // Cache of resolved users: GUID -> (displayName, mail)
        var resolved = new Dictionary<string, (string? DisplayName, string? Mail)>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var graphToken = await GetGraphAccessTokenAsync(tenantId, cancellationToken);
            var httpClient = _httpClientFactory.CreateClient("Purview"); // reuse any HTTP client

            foreach (var userId in guidsToResolve)
            {
                try
                {
                    var url = $"{GraphBaseUrl}/users/{userId}?$select=displayName,mail,userPrincipalName";
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", graphToken);

                    var response = await httpClient.SendAsync(request, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        // Fallback for non-user principals (group/service principal/etc.)
                        var directoryUrl = $"{GraphBaseUrl}/directoryObjects/{userId}";
                        var directoryRequest = new HttpRequestMessage(HttpMethod.Get, directoryUrl);
                        directoryRequest.Headers.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", graphToken);
                        response = await httpClient.SendAsync(directoryRequest, cancellationToken);
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(cancellationToken);
                        var userDoc = JsonDocument.Parse(json);
                        var displayName = GetStringProperty(userDoc.RootElement, "displayName", "name");
                        var mail = GetStringProperty(userDoc.RootElement, "mail", "userPrincipalName", "email");

                        resolved[userId] = (displayName, mail);
                        _logger.LogInformation("Resolved user {UserId} -> {DisplayName} ({Mail})", userId, displayName, mail);
                    }
                    else
                    {
                        _logger.LogWarning("Graph API returned {StatusCode} for user {UserId}",
                            response.StatusCode, userId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve user {UserId} via Graph", userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acquire Graph token for owner resolution — owner names will show GUID/description");
            return;
        }

        // Apply resolved names back to results
        foreach (var result in results)
        {
            foreach (var contact in result.OwnerContacts)
            {
                if (!string.IsNullOrEmpty(contact.Id) &&
                    resolved.TryGetValue(contact.Id, out var resolvedContact))
                {
                    if (!string.IsNullOrEmpty(resolvedContact.DisplayName))
                        contact.Name = resolvedContact.DisplayName;
                    if (!string.IsNullOrEmpty(resolvedContact.Mail))
                        contact.EmailAddress = resolvedContact.Mail;
                }
            }

            if (!string.IsNullOrEmpty(result.OwnerObjectId) &&
                resolved.TryGetValue(result.OwnerObjectId, out var ownerUser))
            {
                if (!string.IsNullOrEmpty(ownerUser.DisplayName))
                    result.Owner = ownerUser.DisplayName;
                if (!string.IsNullOrEmpty(ownerUser.Mail))
                    result.OwnerEmail = ownerUser.Mail;
            }

            var primaryContact = result.OwnerContacts.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.Name) || !string.IsNullOrEmpty(c.EmailAddress));

            if (primaryContact != null)
            {
                if (!string.IsNullOrEmpty(primaryContact.Name))
                    result.Owner = primaryContact.Name;
                if (!string.IsNullOrEmpty(primaryContact.EmailAddress))
                    result.OwnerEmail = primaryContact.EmailAddress;
                if (string.IsNullOrEmpty(result.OwnerObjectId) &&
                    !string.IsNullOrEmpty(primaryContact.Id) &&
                    Guid.TryParse(primaryContact.Id, out _))
                {
                    result.OwnerObjectId = primaryContact.Id;
                }
            }
        }
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

            // Diagnostic: dump raw response on first page so we can see exactly what Purview returns
            if (skip == 0)
            {
                // Truncate to 4000 chars to avoid log bloat but enough to see structure
                var preview = json.Length > 4000 ? json[..4000] + "...(truncated)" : json;
                _logger.LogWarning(
                    "RAW Purview Data Products API response (first page, {Length} chars): {Body}",
                    json.Length, preview);
            }

            var doc = JsonDocument.Parse(json);

            // Log top-level property names to understand the response shape
            var topLevelKeys = string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name));
            _logger.LogInformation("Response top-level keys: [{Keys}]", topLevelKeys);

            if (doc.RootElement.TryGetProperty("value", out var valueArray) && valueArray.ValueKind == JsonValueKind.Array)
            {
                int count = 0;
                foreach (var item in valueArray.EnumerateArray())
                {
                    allItems.Add(item.Clone());
                    count++;
                }

                _logger.LogInformation("Parsed {Count} items from 'value' array (skip={Skip})", count, skip);
                hasMore = count == pageSize; // More pages if we got a full page
                skip += count;
            }
            else
            {
                _logger.LogWarning("Response does NOT contain a 'value' array property. Keys found: [{Keys}]", topLevelKeys);
                hasMore = false;
            }
        }

        _logger.LogInformation("Retrieved {Count} total Data Products from Unified Catalog", allItems.Count);
        return allItems;
    }

    /// <summary>
    /// Maps a Unified Catalog Data Product JSON element to a DataProductSyncResult.
    /// The Purview Data Products API may use different property names/shapes across versions:
    ///   - Owner info: "owners" array (primary), "contacts" array, or "owner" string (fallback)
    ///   - Sensitivity label: object {"id","name"} or plain string
    ///   - Domain: "governanceDomain" or "domain", as object {"id","name"} or plain string
    ///   - Properties may be at top level or nested under a "properties" sub-object
    /// </summary>
    private DataProductSyncResult MapDataProductToSyncResult(
        JsonElement dp,
        Dictionary<string, string> domainNames)
    {
        // Some Azure REST APIs nest resource fields under a "properties" sub-object.
        // If present, merge lookups from both top-level and properties.
        var props = dp.TryGetProperty("properties", out var propsObj) && propsObj.ValueKind == JsonValueKind.Object
            ? propsObj
            : (JsonElement?)null;

        var ownerContacts = ExtractOwnerContacts(dp, props);
        var termsOfUseLinks = ExtractDocumentLinks(dp, props, "termsOfUse");
        var documentationLinks = ExtractDocumentLinks(dp, props, "documentation");

        // Extract owner from owners array (primary) or contacts array (fallback)
        string? owner = null;
        string? ownerEmail = null;
        string? ownerObjectId = ownerContacts
            .Select(c => c.Id)
            .FirstOrDefault(id => !string.IsNullOrEmpty(id) && Guid.TryParse(id, out _));
        owner = ExtractOwnerFromArray(dp, "owners", out ownerEmail);
        if (string.IsNullOrEmpty(owner) && props.HasValue)
            owner = ExtractOwnerFromArray(props.Value, "owners", out ownerEmail);
        if (string.IsNullOrEmpty(owner))
            owner = ExtractOwnerFromArray(dp, "contacts", out ownerEmail);
        if (string.IsNullOrEmpty(owner) && props.HasValue)
            owner = ExtractOwnerFromArray(props.Value, "contacts", out ownerEmail);

        // Extract the owner's Azure AD Object ID from contacts.owner[].id for Graph resolution
        ownerObjectId = ExtractOwnerObjectId(dp, props);

        // Fallback: owner as a direct string property or object with displayName
        if (string.IsNullOrEmpty(owner))
        {
            owner = GetStringProperty(dp, "owner", "dataProductOwner");
            if (string.IsNullOrEmpty(owner) && props.HasValue)
                owner = GetStringProperty(props.Value, "owner", "dataProductOwner");
            // Try object form: {"owner": {"displayName": "...", "mail": "..."}}
            if (string.IsNullOrEmpty(owner))
            {
                var ownerObj = dp.TryGetProperty("owner", out var ov) && ov.ValueKind == JsonValueKind.Object ? ov
                    : (props.HasValue && props.Value.TryGetProperty("owner", out var pov) && pov.ValueKind == JsonValueKind.Object ? pov : (JsonElement?)null);
                if (ownerObj.HasValue)
                {
                    owner = GetStringProperty(ownerObj.Value, "displayName", "name");
                    ownerEmail ??= GetStringProperty(ownerObj.Value, "mail", "email", "userPrincipalName");
                }
            }
        }

        var primaryOwnerContact = ownerContacts.FirstOrDefault();
        if (string.IsNullOrEmpty(owner) && primaryOwnerContact != null)
            owner = primaryOwnerContact.Name ?? primaryOwnerContact.Description ?? primaryOwnerContact.EmailAddress;
        if (string.IsNullOrEmpty(ownerEmail) && primaryOwnerContact != null)
            ownerEmail = primaryOwnerContact.EmailAddress;

        // Note: "audience" (e.g. ["DataScientist"]) is the target audience, NOT the owner.
        // Do not use it as an Owner fallback.

        // Extract governance domain name — try "governanceDomain" then "domain",
        // each may be an object {"id":"...","name":"..."} or a plain string
        string? domain = ExtractDomainName(dp, "governanceDomain")
                      ?? ExtractDomainName(dp, "domain");
        if (string.IsNullOrEmpty(domain) && props.HasValue)
        {
            domain = ExtractDomainName(props.Value, "governanceDomain")
                  ?? ExtractDomainName(props.Value, "domain");
        }

        // If domain is still a GUID, resolve it to the human-readable name from the domain list
        if (!string.IsNullOrEmpty(domain) && Guid.TryParse(domain, out _) &&
            domainNames.TryGetValue(domain, out var resolvedDomainName))
        {
            domain = resolvedDomainName;
        }

        // Extract sensitivity label — may be object {"id":"...","name":"..."} or plain string
        // Also check "additionalProperties" since some fields land there
        string? sensitivityLabel = ExtractStringOrObjectName(dp, "sensitivityLabel");
        if (string.IsNullOrEmpty(sensitivityLabel) && props.HasValue)
            sensitivityLabel = ExtractStringOrObjectName(props.Value, "sensitivityLabel");
        if (string.IsNullOrEmpty(sensitivityLabel) && dp.TryGetProperty("additionalProperties", out var addPropsForLabel) && addPropsForLabel.ValueKind == JsonValueKind.Object)
            sensitivityLabel = ExtractStringOrObjectName(addPropsForLabel, "sensitivityLabel");

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

        // Helper: extract a string-or-object-name property from top level, props, and additionalProperties
        string? StrOrObj(params string[] names)
        {
            foreach (var n in names)
            {
                var v = ExtractStringOrObjectName(dp, n);
                if (!string.IsNullOrEmpty(v)) return v;
                if (props.HasValue)
                {
                    v = ExtractStringOrObjectName(props.Value, n);
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            return null;
        }

        // Helper: try a property at top level, then inside "properties", then inside "additionalProperties"
        string? Str(params string[] names)
        {
            var v = GetStringProperty(dp, names);
            if (string.IsNullOrEmpty(v) && props.HasValue)
                v = GetStringProperty(props.Value, names);
            // Also check inside additionalProperties
            if (string.IsNullOrEmpty(v) && dp.TryGetProperty("additionalProperties", out var ap) && ap.ValueKind == JsonValueKind.Object)
                v = GetStringProperty(ap, names);
            return v;
        }
        int? IntProp(params string[] names)
        {
            var v = ExtractIntProperty(dp, names);
            if (!v.HasValue && props.HasValue)
                v = ExtractIntProperty(props.Value, names);
            // Also check inside additionalProperties
            if (!v.HasValue && dp.TryGetProperty("additionalProperties", out var ap) && ap.ValueKind == JsonValueKind.Object)
                v = ExtractIntProperty(ap, names);
            return v;
        }

        var result = new DataProductSyncResult
        {
            PurviewQualifiedName = GetStringProperty(dp, "id") ?? GetStringProperty(dp, "name") ?? "unknown",
            Name = Str("name", "displayName") ?? "Unnamed",
            Description = StripHtml(Str("description") ?? Str("businessUse")),
            Owner = owner,
            OwnerEmail = ownerEmail,
            OwnerObjectId = ownerObjectId,
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
            BusinessUse = StripHtml(Str("businessUse")),
            Endorsed = endorsed,
            UpdateFrequency = Str("updateFrequency", "refreshFrequency"),
            Documentation = Str("documentation"),
            UseCases = StripHtml(Str("useCases", "useCase", "businessUse")),
            DataQualityScore = IntProp("dataQualityScore", "qualityScore"),
            TermsOfUseUrl = termsOfUseLinks.FirstOrDefault(l => !string.IsNullOrEmpty(l.Url))?.Url
                ?? StrOrObj("termsOfUse", "termsOfUseUrl"),
            TermsOfUseLinks = termsOfUseLinks,
            DocumentationUrl = documentationLinks.FirstOrDefault(l => !string.IsNullOrEmpty(l.Url))?.Url
                ?? StrOrObj("documentation", "documentationUrl", "documentationLink"),
            DocumentationLinks = documentationLinks,
            DataAssets = ExtractDataAssets(dp, props),
            LinkedPurviewAssetIds = ExtractLinkedAssetIds(dp, props),
            TermsOfUseAssetIds = ExtractArrayAssetIds(dp, props, "termsOfUse"),
            DocumentationAssetIds = ExtractArrayAssetIds(dp, props, "documentation"),
            OwnerContacts = ownerContacts
        };

        // Detailed diagnostic logging when key fields are missing
        if (string.IsNullOrEmpty(result.Owner) || string.IsNullOrEmpty(result.SensitivityLabel))
        {
            var topKeys = string.Join(", ", dp.EnumerateObject().Select(p => p.Name));
            var propsKeys = props.HasValue
                ? string.Join(", ", props.Value.EnumerateObject().Select(p => p.Name))
                : "(none)";

            // Dump the raw contacts/audience JSON to understand the structure
            var contactsJson = dp.TryGetProperty("contacts", out var c) ? c.ToString() : "(absent)";
            var audienceJson = dp.TryGetProperty("audience", out var a) ? a.ToString() : "(absent)";
            var addPropsJson2 = dp.TryGetProperty("additionalProperties", out var ap2) ? ap2.ToString() : "(absent)";

            _logger.LogWarning(
                "Data Product '{Name}' has missing fields (Owner={Owner}, SensitivityLabel={Label}). " +
                "Top-level keys: [{TopKeys}]. Properties keys: [{PropsKeys}]. " +
                "contacts: {Contacts}. audience: {Audience}. additionalProperties: {AddProps}",
                result.Name, result.Owner ?? "(null)", result.SensitivityLabel ?? "(null)",
                topKeys, propsKeys, contactsJson, audienceJson, addPropsJson2);
        }

        return result;
    }

    /// <summary>
    /// Extracts data assets from the Data Product's "assets" or "dataAssets" array.
    /// Each asset may have name, type, and description properties.
    /// </summary>
    private static List<DataAssetSyncInfo> ExtractDataAssets(JsonElement dp, JsonElement? props = null)
    {
        var assets = new List<DataAssetSyncInfo>();
        JsonElement assetsArray = default;

        if (dp.TryGetProperty("assets", out assetsArray) && assetsArray.ValueKind == JsonValueKind.Array)
        { /* use it */ }
        else if (dp.TryGetProperty("dataAssets", out assetsArray) && assetsArray.ValueKind == JsonValueKind.Array)
        { /* use it */ }
        else if (props.HasValue && props.Value.TryGetProperty("assets", out assetsArray) && assetsArray.ValueKind == JsonValueKind.Array)
        { /* use it */ }
        else if (props.HasValue && props.Value.TryGetProperty("dataAssets", out assetsArray) && assetsArray.ValueKind == JsonValueKind.Array)
        { /* use it */ }
        else
            return assets;

        foreach (var item in assetsArray.EnumerateArray())
        {
            var name = GetStringProperty(item, "name", "displayName", "qualifiedName") ?? "Unnamed";
            var type = GetStringProperty(item, "type", "typeName", "assetType");
            var desc = GetStringProperty(item, "description");
            assets.Add(new DataAssetSyncInfo { Name = name, Type = type, Description = desc });
        }

        return assets;
    }

    /// <summary>
    /// Extracts dataAssetId references from termsOfUse and documentation arrays
    /// in a data product JSON element (from the list endpoint).
    /// </summary>
    private static List<string> ExtractLinkedAssetIds(JsonElement dp, JsonElement? props = null)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void ExtractFromArray(string propertyName)
        {
            if (TryGetArrayProperty(dp, props, propertyName, out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.TryGetProperty("dataAssetId", out var idProp))
                    {
                        var id = idProp.GetString();
                        if (!string.IsNullOrEmpty(id))
                            ids.Add(id);
                    }
                }
            }
        }

        ExtractFromArray("termsOfUse");
        ExtractFromArray("documentation");

        return ids.ToList();
    }

    /// <summary>
    /// Extracts dataAssetId references from a single named array property in a data product JSON element.
    /// Used to separately track which asset IDs came from "termsOfUse" vs "documentation".
    /// </summary>
    private static List<string> ExtractArrayAssetIds(JsonElement dp, JsonElement? props, string propertyName)
    {
        var ids = new List<string>();
        if (TryGetArrayProperty(dp, props, propertyName, out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("dataAssetId", out var idProp))
                {
                    var id = idProp.GetString();
                    if (!string.IsNullOrEmpty(id))
                        ids.Add(id);
                }
            }
        }
        return ids;
    }

    private static List<DataProductOwnerContactInfo> ExtractOwnerContacts(JsonElement dp, JsonElement? props)
    {
        var contacts = new List<DataProductOwnerContactInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddContact(JsonElement item)
        {
            var contact = MapOwnerContact(item);
            if (contact == null)
                return;

            var key = string.Join("|",
                contact.Id ?? string.Empty,
                contact.EmailAddress ?? string.Empty,
                contact.Name ?? string.Empty,
                contact.Description ?? string.Empty);

            if (seen.Add(key))
                contacts.Add(contact);
        }

        void AddFromSource(JsonElement source)
        {
            if (source.TryGetProperty("contacts", out var contactsObject) && contactsObject.ValueKind == JsonValueKind.Object)
            {
                foreach (var subKey in new[] { "owner", "owners" })
                {
                    if (contactsObject.TryGetProperty(subKey, out var ownerArray) && ownerArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in ownerArray.EnumerateArray())
                            AddContact(item);
                    }
                }
            }

            foreach (var propertyName in new[] { "owners", "owner" })
            {
                if (!source.TryGetProperty(propertyName, out var ownerValue))
                    continue;

                if (ownerValue.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in ownerValue.EnumerateArray())
                        AddContact(item);
                }
                else
                {
                    AddContact(ownerValue);
                }
            }
        }

        AddFromSource(dp);
        if (props.HasValue)
            AddFromSource(props.Value);

        return contacts;
    }

    private static DataProductOwnerContactInfo? MapOwnerContact(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            var value = item.GetString();
            if (string.IsNullOrEmpty(value))
                return null;

            return new DataProductOwnerContactInfo
            {
                Name = value.Contains('@') ? null : value,
                EmailAddress = value.Contains('@') ? value : null
            };
        }

        if (item.ValueKind != JsonValueKind.Object)
            return null;

        var name = GetStringProperty(item, "displayName", "name", "contactName");
        var email = GetStringProperty(item, "mail", "email", "userPrincipalName", "contactEmail");
        var id = GetStringProperty(item, "id");
        var description = GetStringProperty(item, "description");

        // Purview contact entries often carry a GUID id + role description.
        // In that case, person name/email should come from Entra lookup, not this payload.
        if (!string.IsNullOrEmpty(id) && Guid.TryParse(id, out _))
        {
            if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(description))
            {
                description = name;
            }

            name = null;
            email = null;
        }

        if (string.IsNullOrEmpty(name) && item.TryGetProperty("info", out var infoObj) && infoObj.ValueKind == JsonValueKind.Object)
        {
            name = GetStringProperty(infoObj, "displayName", "name");
            email ??= GetStringProperty(infoObj, "mail", "email", "userPrincipalName");
        }

        var contact = new DataProductOwnerContactInfo
        {
            Id = id,
            Description = description,
            Name = name,
            EmailAddress = email
        };

        if (string.IsNullOrEmpty(contact.Id) &&
            string.IsNullOrEmpty(contact.Description) &&
            string.IsNullOrEmpty(contact.Name) &&
            string.IsNullOrEmpty(contact.EmailAddress))
        {
            return null;
        }

        return contact;
    }

    private static List<DataProductLinkInfo> ExtractDocumentLinks(JsonElement dp, JsonElement? props, string propertyName)
    {
        var links = new List<DataProductLinkInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddLink(DataProductLinkInfo? link)
        {
            if (link == null || (string.IsNullOrEmpty(link.Name) && string.IsNullOrEmpty(link.Url) && string.IsNullOrEmpty(link.DataAssetId)))
                return;

            var key = string.Join("|", link.DataAssetId ?? string.Empty, link.Name ?? string.Empty, link.Url ?? string.Empty);
            if (seen.Add(key))
                links.Add(link);
        }

        void AddFromValue(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                    AddFromValue(item);
                return;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var url = value.GetString();
                if (!string.IsNullOrEmpty(url))
                    AddLink(new DataProductLinkInfo { Url = url });
                return;
            }

            if (value.ValueKind != JsonValueKind.Object)
                return;

            AddLink(new DataProductLinkInfo
            {
                Name = GetStringProperty(value, "name", "displayName", "title"),
                Url = GetStringProperty(value, "url", "href", "link"),
                DataAssetId = GetStringProperty(value, "dataAssetId")
            });
        }

        if (dp.TryGetProperty(propertyName, out var topLevelValue))
            AddFromValue(topLevelValue);

        if (props.HasValue && props.Value.TryGetProperty(propertyName, out var propsValue))
            AddFromValue(propsValue);

        return links;
    }

    private static bool TryGetArrayProperty(JsonElement dp, JsonElement? props, string propertyName, out JsonElement array)
    {
        if (dp.TryGetProperty(propertyName, out array) && array.ValueKind == JsonValueKind.Array)
            return true;

        if (props.HasValue && props.Value.TryGetProperty(propertyName, out array) && array.ValueKind == JsonValueKind.Array)
            return true;

        array = default;
        return false;
    }

    /// <summary>
    /// Extracts an integer value from one of the named properties.
    /// </summary>
    private static int? ExtractIntProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var intVal))
                    return intVal;
                if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var parsed))
                    return parsed;
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts owner display name and email from a named array property (e.g. "owners" or "contacts").
    /// Handles multiple Purview response shapes:
    ///   - [{"displayName":"...","mail":"..."}]
    ///   - [{"name":"...","email":"..."}]
    ///   - [{"info":{"displayName":"...","mail":"..."}}]
    ///   - [{"contactName":"...","contactEmail":"..."}]
    ///   - ["email@example.com"]  (plain strings)
    ///   - {"id":"...","displayName":"...","mail":"..."} (single object, not array)
    ///   - {"owner":[{"id":"...","description":"..."}], "expert":[...]} (Purview governance contacts)
    /// </summary>
    private static string? ExtractOwnerFromArray(JsonElement dp, string arrayPropertyName, out string? email)
    {
        email = null;
        if (!dp.TryGetProperty(arrayPropertyName, out var val))
            return null;

        // Handle single object form (not an array)
        if (val.ValueKind == JsonValueKind.Object)
        {
            // Purview governance shape: contacts is {"owner":[...], "expert":[...]}
            // Try the "owner" sub-array first, then other known sub-keys
            foreach (var subKey in new[] { "owner", "owners", "expert", "experts" })
            {
                if (val.TryGetProperty(subKey, out var subArray) && subArray.ValueKind == JsonValueKind.Array)
                {
                    var result = ExtractFirstPersonFromArray(subArray, out email);
                    if (!string.IsNullOrEmpty(result)) return result;
                }
            }

            // Fallback: try treating the object itself as a person record
            var name = GetStringProperty(val, "displayName", "name", "contactName");
            email = GetStringProperty(val, "mail", "email", "userPrincipalName", "contactEmail");
            // Check for nested "info" sub-object
            if (string.IsNullOrEmpty(name) && val.TryGetProperty("info", out var infoObj) && infoObj.ValueKind == JsonValueKind.Object)
            {
                name = GetStringProperty(infoObj, "displayName", "name");
                email ??= GetStringProperty(infoObj, "mail", "email", "userPrincipalName");
            }
            return name ?? email;
        }

        // Handle plain string form
        if (val.ValueKind == JsonValueKind.String)
        {
            var str = val.GetString();
            if (!string.IsNullOrEmpty(str))
            {
                if (str.Contains('@')) email = str;
                return str;
            }
            return null;
        }

        if (val.ValueKind != JsonValueKind.Array)
            return null;

        return ExtractFirstPersonFromArray(val, out email);
    }

    /// <summary>
    /// Extracts the first person's name/email from a JSON array of person objects or strings.
    /// </summary>
    private static string? ExtractFirstPersonFromArray(JsonElement array, out string? email)
    {
        email = null;
        foreach (var item in array.EnumerateArray())
        {
            // Array of plain strings
            if (item.ValueKind == JsonValueKind.String)
            {
                var str = item.GetString();
                if (!string.IsNullOrEmpty(str))
                {
                    if (str.Contains('@')) email = str;
                    return str;
                }
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
                continue;

            // Direct properties
            var name2 = GetStringProperty(item, "displayName", "name", "contactName");
            var mail = GetStringProperty(item, "mail", "email", "userPrincipalName", "contactEmail");

            // Nested "info" sub-object
            if (string.IsNullOrEmpty(name2) && item.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Object)
            {
                name2 = GetStringProperty(info, "displayName", "name");
                mail ??= GetStringProperty(info, "mail", "email", "userPrincipalName");
            }

            if (!string.IsNullOrEmpty(name2) || !string.IsNullOrEmpty(mail))
            {
                email = mail;
                return name2 ?? mail; // Fall back to email if no display name
            }

            // Purview governance shape: {"id": "<guid>", "description": "Creator"}
            // The id is a user/principal GUID — store it so it can be resolved later
            var idVal = GetStringProperty(item, "id");
            var descVal = GetStringProperty(item, "description");
            if (!string.IsNullOrEmpty(idVal))
            {
                // Return the description (e.g. "Creator") as a label, with the GUID as fallback
                return descVal ?? idVal;
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

    /// <summary>
    /// Extracts the owner's Azure AD Object ID (GUID) from the contacts.owner array.
    /// The Purview governance contacts shape is: {"owner":[{"id":"<guid>","description":"Creator"}]}
    /// </summary>
    private static string? ExtractOwnerObjectId(JsonElement dp, JsonElement? props)
    {
        // Try contacts.owner[0].id at top level and in properties
        foreach (var source in props.HasValue ? new[] { dp, props.Value } : new[] { dp })
        {
            if (source.TryGetProperty("contacts", out var contacts) && contacts.ValueKind == JsonValueKind.Object)
            {
                foreach (var subKey in new[] { "owner", "owners" })
                {
                    if (contacts.TryGetProperty(subKey, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in arr.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                var id = GetStringProperty(item, "id");
                                if (!string.IsNullOrEmpty(id) && Guid.TryParse(id, out _))
                                    return id;
                            }
                        }
                    }
                }
            }

            // Also try owners[0].id and owner.id directly
            foreach (var arrayName in new[] { "owners", "owner" })
            {
                if (source.TryGetProperty(arrayName, out var arr))
                {
                    if (arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in arr.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                var id = GetStringProperty(item, "id");
                                if (!string.IsNullOrEmpty(id) && Guid.TryParse(id, out _))
                                    return id;
                            }
                        }
                    }
                    else if (arr.ValueKind == JsonValueKind.Object)
                    {
                        var id = GetStringProperty(arr, "id");
                        if (!string.IsNullOrEmpty(id) && Guid.TryParse(id, out _))
                            return id;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Strips HTML tags from a string, returning plain text.
    /// Handles Purview fields like description that are wrapped in HTML (e.g. "&lt;div&gt;Some text&lt;/div&gt;").
    /// </summary>
    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return html;

        // Remove HTML tags
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        // Decode common HTML entities
        text = text.Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'")
                   .Replace("&nbsp;", " ");
        // Collapse whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    // ── Data Assets scanning ───────────────────────────────────────────────

    public async Task<List<DataAssetSyncResult>> ScanForDataAssetsAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning Purview Unified Catalog for Data Assets (tenant: {Tenant})", tenantId);

        var accessToken = await GetAccessTokenAsync(tenantId, cancellationToken);
        var httpClient = _httpClientFactory.CreateClient("Purview");

        var rawAssets = await FetchDataAssetsFromCatalogAsync(httpClient, accessToken, cancellationToken);

        var results = rawAssets.Select(MapDataAssetToSyncResult).ToList();

        _logger.LogInformation("Found {Count} Data Assets in Unified Catalog", results.Count);
        return results;
    }

    private async Task<List<JsonElement>> FetchDataAssetsFromCatalogAsync(
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
            var url = $"{UnifiedCatalogBaseUrl}/datagovernance/catalog/dataAssets" +
                      $"?api-version={DataProductsApiVersion}&top={pageSize}&skip={skip}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            _logger.LogInformation("Fetching Data Assets from Unified Catalog (skip={Skip})", skip);

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Data Assets API returned {StatusCode}: {Body}", response.StatusCode, errorBody);

                if (allItems.Count == 0)
                {
                    throw new HttpRequestException(
                        $"Data Assets API returned {(int)response.StatusCode} ({response.StatusCode}): {errorBody}");
                }
                break;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("value", out var valueArray) && valueArray.ValueKind == JsonValueKind.Array)
            {
                int count = 0;
                foreach (var item in valueArray.EnumerateArray())
                {
                    allItems.Add(item.Clone());
                    count++;
                }

                _logger.LogInformation("Parsed {Count} data assets (skip={Skip})", count, skip);
                hasMore = count == pageSize;
                skip += count;
            }
            else
            {
                _logger.LogWarning("Data Assets API response has no 'value' array");
                hasMore = false;
            }
        }

        return allItems;
    }

    private DataAssetSyncResult MapDataAssetToSyncResult(JsonElement asset)
    {
        var result = new DataAssetSyncResult
        {
            PurviewAssetId = GetStringProperty(asset, "id") ?? string.Empty,
            Name = GetStringProperty(asset, "name") ?? "unknown",
            Type = GetStringProperty(asset, "type"),
            Description = StripHtml(GetStringProperty(asset, "description")),
        };

        // Extract source properties
        if (asset.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object)
        {
            result.AssetType = GetStringProperty(source, "assetType");
            result.FullyQualifiedName = GetStringProperty(source, "fqn");
            result.AccountName = GetStringProperty(source, "accountName");
            result.SourceWorkspaceId = GetStringProperty(source, "workspaceId");

            if (source.TryGetProperty("lastRefreshedAt", out var refreshed))
            {
                if (DateTime.TryParse(refreshed.GetString(), out var dt))
                    result.LastRefreshedAt = dt;
            }

            // Extract workspace name from assetAttributes
            if (source.TryGetProperty("assetAttributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
            {
                result.WorkspaceName = GetStringProperty(attrs, "workspaceName");
                result.SourceWorkspaceId ??= GetStringProperty(attrs, "workspaceId");
            }
        }

        // Extract system data
        if (asset.TryGetProperty("systemData", out var sysData) && sysData.ValueKind == JsonValueKind.Object)
        {
            result.ProvisioningState = GetStringProperty(sysData, "provisioningState");

            if (sysData.TryGetProperty("createdAt", out var created) && DateTime.TryParse(created.GetString(), out var cdt))
                result.PurviewCreatedAt = cdt;

            if (sysData.TryGetProperty("lastModifiedAt", out var modified) && DateTime.TryParse(modified.GetString(), out var mdt))
                result.PurviewLastModifiedAt = mdt;
        }

        // Serialize contacts and classifications as JSON
        if (asset.TryGetProperty("contacts", out var contacts))
            result.ContactsJson = contacts.GetRawText();

        if (asset.TryGetProperty("classifications", out var classifications))
            result.ClassificationsJson = classifications.GetRawText();

        return result;
    }

    /// <inheritdoc />
    public async Task<List<string>> FetchProductLinkedAssetIdsAsync(
        string purviewProductId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var linkedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var accessToken = await GetAccessTokenAsync(tenantId, cancellationToken);
            var httpClient = _httpClientFactory.CreateClient("Purview");

            var url = $"https://api.purview-service.microsoft.com/datagovernance/catalog/dataProducts/{purviewProductId}?api-version=2025-09-15-preview";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch product detail for {ProductId}: {Status}",
                    purviewProductId, response.StatusCode);
                return linkedIds.ToList();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract dataAssetId from termsOfUse array
            if (root.TryGetProperty("termsOfUse", out var termsOfUse) && termsOfUse.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in termsOfUse.EnumerateArray())
                {
                    if (item.TryGetProperty("dataAssetId", out var assetIdProp))
                    {
                        var assetId = assetIdProp.GetString();
                        if (!string.IsNullOrEmpty(assetId))
                            linkedIds.Add(assetId);
                    }
                }
            }

            // Extract dataAssetId from documentation array
            if (root.TryGetProperty("documentation", out var documentation) && documentation.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in documentation.EnumerateArray())
                {
                    if (item.TryGetProperty("dataAssetId", out var assetIdProp))
                    {
                        var assetId = assetIdProp.GetString();
                        if (!string.IsNullOrEmpty(assetId))
                            linkedIds.Add(assetId);
                    }
                }
            }

            _logger.LogInformation(
                "Product {ProductId}: found {Count} linked asset IDs from detail endpoint",
                purviewProductId, linkedIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error fetching product detail for linked asset IDs: {ProductId}",
                purviewProductId);
        }

        return linkedIds.ToList();
    }
}
