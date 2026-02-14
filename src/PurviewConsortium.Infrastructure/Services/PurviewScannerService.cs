using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Infrastructure.Services;

public class PurviewScannerService : IPurviewScannerService
{
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
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning Purview account {Account} in tenant {Tenant} for shareable Data Products",
            purviewAccountName, tenantId);

        var results = new List<DataProductSyncResult>();

        try
        {
            var accessToken = await GetAccessTokenAsync(tenantId, cancellationToken);
            var httpClient = _httpClientFactory.CreateClient("Purview");

            var baseUrl = $"https://{purviewAccountName}.purview.azure.com";

            // Try Data Products API first
            var dataProducts = await FetchDataProductsAsync(httpClient, baseUrl, accessToken, cancellationToken);

            if (dataProducts != null)
            {
                foreach (var dp in dataProducts)
                {
                    // Filter for Consortium-Shareable glossary term
                    var glossaryTerms = ExtractGlossaryTerms(dp);
                    if (!glossaryTerms.Any(t => t.Equals("Consortium-Shareable", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    results.Add(MapToSyncResult(dp, glossaryTerms));
                }
            }

            _logger.LogInformation("Found {Count} shareable Data Products in {Account}",
                results.Count, purviewAccountName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning Purview account {Account}", purviewAccountName);
            throw;
        }

        return results;
    }

    private async Task<string> GetAccessTokenAsync(string tenantId, CancellationToken cancellationToken)
    {
        var clientId = _configuration["AzureAd:ClientId"];
        var clientSecret = _configuration["AzureAd:ClientSecret"];

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var tokenResult = await credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { "https://purview.azure.net/.default" }),
            cancellationToken);

        return tokenResult.Token;
    }

    private async Task<List<JsonElement>?> FetchDataProductsAsync(
        HttpClient httpClient,
        string baseUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{baseUrl}/dataproducts/api/data-products?api-version=2024-03-01-preview");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Data Products API returned {StatusCode}, falling back to Catalog Search",
                    response.StatusCode);
                return await FallbackToCatalogSearchAsync(httpClient, baseUrl, accessToken, cancellationToken);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            var items = new List<JsonElement>();
            if (doc.RootElement.TryGetProperty("value", out var valueArray))
            {
                foreach (var item in valueArray.EnumerateArray())
                {
                    items.Add(item.Clone());
                }
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Data Products API failed, falling back to Catalog Search");
            return await FallbackToCatalogSearchAsync(httpClient, baseUrl, accessToken, cancellationToken);
        }
    }

    private async Task<List<JsonElement>?> FallbackToCatalogSearchAsync(
        HttpClient httpClient,
        string baseUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var searchBody = new
        {
            keywords = "*",
            filter = new
            {
                and = new object[]
                {
                    new { glossaryTerms = new[] { "Consortium-Shareable" } }
                }
            },
            limit = 1000
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl}/catalog/api/search/query?api-version=2023-09-01");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(searchBody),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        var items = new List<JsonElement>();
        if (doc.RootElement.TryGetProperty("value", out var valueArray))
        {
            foreach (var item in valueArray.EnumerateArray())
            {
                items.Add(item.Clone());
            }
        }

        return items;
    }

    private static List<string> ExtractGlossaryTerms(JsonElement element)
    {
        var terms = new List<string>();

        if (element.TryGetProperty("glossaryTerms", out var termsArray) ||
            element.TryGetProperty("term", out termsArray) ||
            element.TryGetProperty("meanings", out termsArray))
        {
            if (termsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var term in termsArray.EnumerateArray())
                {
                    var name = term.TryGetProperty("displayText", out var displayText)
                        ? displayText.GetString()
                        : term.TryGetProperty("name", out var nameVal)
                            ? nameVal.GetString()
                            : term.GetString();

                    if (!string.IsNullOrEmpty(name))
                        terms.Add(name);
                }
            }
        }

        return terms;
    }

    private static DataProductSyncResult MapToSyncResult(JsonElement element, List<string> glossaryTerms)
    {
        return new DataProductSyncResult
        {
            PurviewQualifiedName = GetStringProperty(element, "qualifiedName", "id", "name") ?? "unknown",
            Name = GetStringProperty(element, "name", "displayName") ?? "Unnamed",
            Description = GetStringProperty(element, "description"),
            Owner = GetStringProperty(element, "owner", "ownerName"),
            OwnerEmail = GetStringProperty(element, "ownerEmail"),
            SourceSystem = GetStringProperty(element, "sourceSystem", "sourceType", "typeName"),
            SchemaJson = ExtractSchemaJson(element),
            Classifications = ExtractClassifications(element),
            GlossaryTerms = glossaryTerms,
            SensitivityLabel = GetStringProperty(element, "sensitivityLabel", "sensitivity"),
            PurviewLastModified = ExtractDateTime(element, "lastModifiedTS", "updateTime", "modifiedTime")
        };
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

    private static List<string> ExtractClassifications(JsonElement element)
    {
        var classifications = new List<string>();

        if (element.TryGetProperty("classifications", out var classArray) &&
            classArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in classArray.EnumerateArray())
            {
                var name = c.TryGetProperty("typeName", out var typeName)
                    ? typeName.GetString()
                    : c.GetString();
                if (!string.IsNullOrEmpty(name))
                    classifications.Add(name);
            }
        }

        return classifications;
    }

    private static string? ExtractSchemaJson(JsonElement element)
    {
        if (element.TryGetProperty("schema", out var schema) ||
            element.TryGetProperty("columns", out schema) ||
            element.TryGetProperty("schemaElements", out schema))
        {
            return schema.GetRawText();
        }
        return null;
    }

    private static DateTime? ExtractDateTime(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var dt))
                    return dt;
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var epoch))
                    return DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime;
            }
        }
        return null;
    }
}
