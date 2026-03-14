using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Infrastructure.Services;

/// <summary>
/// Manages Purview Data Access subscriptions via the
/// datagovernance/dataaccess REST API on the purview-service.microsoft.com endpoint.
///
/// API base: https://{tenantId}-api.purview-service.microsoft.com/datagovernance/dataaccess
/// </summary>
public class PurviewDataAccessService : IPurviewDataAccessService
{
    private const string PurviewScope = "https://purview.azure.net/.default";
    private const string DataAccessApiVersion = "2023-10-01-preview";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PurviewDataAccessService> _logger;

    public PurviewDataAccessService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PurviewDataAccessService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CreateDataSubscriptionResult> CreateDataSubscriptionAsync(
        string tenantId,
        string subscriptionId,
        string dataProductId,
        string subscriberObjectId,
        string identityType,
        string businessJustification,
        string purpose,
        string? userAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating Purview data subscription {SubscriptionId} for data product {DataProductId}, " +
            "subscriber {SubscriberObjectId} in tenant {TenantId}",
            subscriptionId, dataProductId, subscriberObjectId, tenantId);

        if (!Guid.TryParse(dataProductId, out _))
        {
            return new CreateDataSubscriptionResult
            {
                Success = false,
                ErrorMessage = $"Invalid Purview data product ID: '{dataProductId}'. Expected a GUID."
            };
        }

        if (!Guid.TryParse(subscriberObjectId, out _))
        {
            return new CreateDataSubscriptionResult
            {
                Success = false,
                ErrorMessage = $"Invalid subscriber object ID: '{subscriberObjectId}'. Expected an Entra object ID GUID."
            };
        }

        try
        {
            var accessToken = await GetAccessTokenAsync(tenantId, userAccessToken, cancellationToken);
            var baseUrl = BuildBaseUrl(tenantId);
            var url = $"{baseUrl}/dataSubscriptions/{Uri.EscapeDataString(subscriptionId)}?api-version={DataAccessApiVersion}";
            var payload = new
            {
                dataSubscription = new
                {
                    subscriberIdentity = new
                    {
                        identityType,
                        objectId = subscriberObjectId
                    },
                    dataProductId,
                    policySetValues = new
                    {
                        businessJustification,
                        purpose
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var httpClient = _httpClientFactory.CreateClient("Purview");
            var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Purview Data Access API returned {Status} when creating subscription {SubscriptionId}: {Body}",
                    response.StatusCode, subscriptionId, responseBody);

                return new CreateDataSubscriptionResult
                {
                    Success = false,
                    ErrorMessage = $"Purview API error ({(int)response.StatusCode}): {responseBody}"
                };
            }

            var subscription = ParseSubscriptionItem(responseBody, subscriptionId);

            _logger.LogInformation(
                "Data subscription {SubscriptionId} created successfully for data product {DataProductId}",
                subscriptionId, dataProductId);

            return new CreateDataSubscriptionResult
            {
                Success = true,
                SubscriptionId = subscriptionId,
                Subscription = subscription
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create Purview data subscription {SubscriptionId} for data product {DataProductId}",
                subscriptionId, dataProductId);

            return new CreateDataSubscriptionResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ListDataSubscriptionsResult> ListUserDataSubscriptionsAsync(
        string tenantId,
        string? subscriberObjectId = null,
        string? dataProductId = null,
        string? userAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Listing Purview data subscriptions in tenant {TenantId} " +
            "(subscriberObjectId={SubscriberId}, dataProductId={DataProductId})",
            tenantId, subscriberObjectId ?? "(all)", dataProductId ?? "(all)");

        try
        {
            var accessToken = await GetAccessTokenAsync(tenantId, userAccessToken, cancellationToken);
            var baseUrl = BuildBaseUrl(tenantId);
            var url = $"{baseUrl}/dataSubscriptions?api-version={DataAccessApiVersion}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var httpClient = _httpClientFactory.CreateClient("Purview");
            var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Purview Data Access API returned {Status} when listing subscriptions: {Body}",
                    response.StatusCode, responseBody);

                return new ListDataSubscriptionsResult
                {
                    Success = false,
                    ErrorMessage = $"Purview API error ({(int)response.StatusCode}): {responseBody}"
                };
            }

            var all = ParseSubscriptionList(responseBody);

            // Client-side filtering — apply subscriber and data product filters if provided
            if (!string.IsNullOrWhiteSpace(subscriberObjectId))
                all = all.Where(s => string.Equals(s.SubscriberObjectId, subscriberObjectId, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(dataProductId))
                all = all.Where(s => string.Equals(s.DataProductId, dataProductId, StringComparison.OrdinalIgnoreCase)).ToList();

            _logger.LogInformation(
                "Retrieved {Count} data subscription(s) after filtering for tenant {TenantId}",
                all.Count, tenantId);

            return new ListDataSubscriptionsResult
            {
                Success = true,
                Subscriptions = all
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to list Purview data subscriptions in tenant {TenantId}", tenantId);

            return new ListDataSubscriptionsResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static string BuildBaseUrl(string tenantId) =>
        $"https://{tenantId}-api.purview-service.microsoft.com/datagovernance/dataaccess";

    /// <summary>
    /// Parses the JSON response body of a single subscription PUT into a <see cref="DataSubscriptionItem"/>.
    /// Falls back to a minimal object using the provided ID if parsing fails.
    /// </summary>
    private DataSubscriptionItem ParseSubscriptionItem(string json, string fallbackId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return MapSubscriptionElement(UnwrapSubscriptionElement(doc.RootElement), fallbackId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse subscription response body; returning minimal item.");
            return new DataSubscriptionItem { Id = fallbackId };
        }
    }

    /// <summary>
    /// Parses the JSON response body of the list endpoint into a list of <see cref="DataSubscriptionItem"/>.
    /// Expects either a top-level array or a wrapper object with a "value" array (OData paging convention).
    /// </summary>
    private List<DataSubscriptionItem> ParseSubscriptionList(string json)
    {
        var results = new List<DataSubscriptionItem>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // OData envelope: { "value": [ ... ] }
            var items = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("value", out var valueArr) ? valueArr : default;

            if (items.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var element in items.EnumerateArray())
            {
                var subscriptionElement = UnwrapSubscriptionElement(element);
                var id = subscriptionElement.TryGetProperty("id", out var idVal) ? idVal.GetString() ?? string.Empty : string.Empty;
                results.Add(MapSubscriptionElement(subscriptionElement, id));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse data subscription list response.");
        }

        return results;
    }

    private static DataSubscriptionItem MapSubscriptionElement(JsonElement el, string fallbackId)
    {
        var item = new DataSubscriptionItem
        {
            Id = el.TryGetProperty("id", out var id) ? id.GetString() ?? fallbackId : fallbackId,
            DataProductId = el.TryGetProperty("dataProductId", out var dpId) ? dpId.GetString() ?? string.Empty : string.Empty,
            Status = el.TryGetProperty("status", out var status) ? status.GetString() : null
        };

        if (el.TryGetProperty("subscriberIdentity", out var identity))
        {
            item.SubscriberObjectId = identity.TryGetProperty("objectId", out var oid) ? oid.GetString() ?? string.Empty : string.Empty;
            item.IdentityType = identity.TryGetProperty("identityType", out var itype) ? itype.GetString() ?? string.Empty : string.Empty;
        }

        if (el.TryGetProperty("policySetValues", out var psv))
        {
            item.BusinessJustification = psv.TryGetProperty("businessJustification", out var bj) ? bj.GetString() : null;
            item.UseCase = psv.TryGetProperty("useCase", out var uc) ? uc.GetString() : null;
        }

        if (el.TryGetProperty("createdDate", out var created) && created.ValueKind == JsonValueKind.String)
            item.CreatedDate = created.TryGetDateTime(out var dt) ? dt : null;

        if (el.TryGetProperty("modifiedDate", out var modified) && modified.ValueKind == JsonValueKind.String)
            item.ModifiedDate = modified.TryGetDateTime(out var dt2) ? dt2 : null;

        return item;
    }

    private static JsonElement UnwrapSubscriptionElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("dataSubscription", out var wrapped) &&
            wrapped.ValueKind == JsonValueKind.Object)
        {
            return wrapped;
        }

        return element;
    }

    private async Task<string> GetAccessTokenAsync(
        string tenantId,
        string? userAccessToken,
        CancellationToken cancellationToken)
    {
        var clientId = _configuration["AzureAd:ClientId"];
        var clientSecret = _configuration["AzureAd:ClientSecret"];

        if (!string.IsNullOrEmpty(userAccessToken))
        {
            _logger.LogDebug("Acquiring Purview Data Access token via OBO flow for tenant {TenantId}", tenantId);
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

                return oboResult.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "OBO token acquisition failed for tenant {TenantId} ({ExceptionType}: {Message}). " +
                    "Falling back to client credentials.",
                    tenantId, ex.GetType().Name, ex.Message);
            }
        }

        // Fallback: client credentials
        _logger.LogDebug("Acquiring Purview Data Access token via client credentials for tenant {TenantId}", tenantId);
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var tokenResult = await credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { PurviewScope }),
            cancellationToken);

        return tokenResult.Token;
    }
}
