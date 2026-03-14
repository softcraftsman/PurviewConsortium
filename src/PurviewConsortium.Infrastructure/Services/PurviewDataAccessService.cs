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

            // Payload matches the Purview Data Access UI contract exactly — no wrapper object;
            // dataProductId, subscriberIdentity, and policySetValues are all at the root level.
            var payload = new
            {
                dataProductId,
                subscriberIdentity = new
                {
                    identityType,
                    objectId = subscriberObjectId
                },
                policySetValues = new
                {
                    businessJustification,
                    useCase = purpose
                }
            };

            var payloadJson = JsonSerializer.Serialize(payload);
            _logger.LogDebug(
                "Purview Data Access create subscription payload: {Payload}", payloadJson);

            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(
                    payloadJson,
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

    /// <inheritdoc/>
    public async Task<GetDataSubscriptionResult> GetDataSubscriptionAsync(
        string tenantId,
        string subscriptionId,
        string? userAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Fetching Purview data subscription {SubscriptionId} in tenant {TenantId}",
            subscriptionId, tenantId);

        try
        {
            var accessToken = await GetAccessTokenAsync(tenantId, userAccessToken, cancellationToken);
            var baseUrl = BuildBaseUrl(tenantId);
            var url = $"{baseUrl}/dataSubscriptions/{Uri.EscapeDataString(subscriptionId)}?api-version={DataAccessApiVersion}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var httpClient = _httpClientFactory.CreateClient("Purview");
            var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Purview Data Access API returned {Status} when fetching subscription {SubscriptionId}: {Body}",
                    response.StatusCode, subscriptionId, responseBody);
                return new GetDataSubscriptionResult
                {
                    Success = false,
                    ErrorMessage = $"Purview API error ({(int)response.StatusCode}): {responseBody}"
                };
            }

            var item = ParseSubscriptionItem(responseBody, subscriptionId);
            return new GetDataSubscriptionResult { Success = true, Subscription = item };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to fetch Purview data subscription {SubscriptionId} in tenant {TenantId}",
                subscriptionId, tenantId);
            return new GetDataSubscriptionResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public async Task<CancelDataSubscriptionResult> CancelDataSubscriptionAsync(
        string tenantId,
        string subscriptionId,
        string? userAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Cancelling Purview data subscription {SubscriptionId} in tenant {TenantId}",
            subscriptionId, tenantId);

        try
        {
            var accessToken = await GetAccessTokenAsync(tenantId, userAccessToken, cancellationToken);
            var baseUrl = BuildBaseUrl(tenantId);

            var cancelUrls = new[]
            {
                $"{baseUrl}/dataSubscriptions/{Uri.EscapeDataString(subscriptionId)}:cancel?api-version={DataAccessApiVersion}",
                $"{baseUrl}/dataSubscriptions/{Uri.EscapeDataString(subscriptionId)}/cancel?api-version={DataAccessApiVersion}"
            };

            var httpClient = _httpClientFactory.CreateClient("Purview");

            foreach (var url in cancelUrls)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return new CancelDataSubscriptionResult { Success = true };
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogDebug(
                        "Purview cancel endpoint not found at {Url} for subscription {SubscriptionId}. Trying next pattern.",
                        url, subscriptionId);
                    continue;
                }

                _logger.LogWarning(
                    "Purview cancel attempt failed at {Url}. Status={Status}, Body={Body}",
                    url, response.StatusCode, responseBody);
            }

            return new CancelDataSubscriptionResult
            {
                Success = false,
                ErrorMessage = "Purview did not accept subscription cancellation request."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to cancel Purview data subscription {SubscriptionId} in tenant {TenantId}",
                subscriptionId, tenantId);
            return new CancelDataSubscriptionResult
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
            return MapSubscriptionElement(doc.RootElement, fallbackId);
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
                var id = element.TryGetProperty("dataSubscriptionId", out var dsId) ? dsId.GetString() ?? string.Empty
                    : element.TryGetProperty("id", out var idVal) ? idVal.GetString() ?? string.Empty
                    : string.Empty;
                results.Add(MapSubscriptionElement(element, id));
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
        // Response fields per the Purview Data Access API sample:
        //   root-level id field is "dataSubscriptionId"; status is "subscriptionStatus"; date is "createdAt"
        var idStr = el.TryGetProperty("dataSubscriptionId", out var dsId) ? dsId.GetString()
            : el.TryGetProperty("id", out var id) ? id.GetString()
            : null;

        var item = new DataSubscriptionItem
        {
            Id = idStr ?? fallbackId,
            DataProductId = el.TryGetProperty("dataProductId", out var dpId) ? dpId.GetString() ?? string.Empty : string.Empty,
            Status = ResolveSubscriptionStatus(el)
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

        // "createdAt" per the live API response (fall back to "createdDate" for any legacy shape)
        var createdKey = el.TryGetProperty("createdAt", out var createdAt) ? createdAt
            : el.TryGetProperty("createdDate", out var createdDate) ? createdDate
            : default;
        if (createdKey.ValueKind == JsonValueKind.String)
            item.CreatedDate = createdKey.TryGetDateTime(out var dt) ? dt : null;

        if (el.TryGetProperty("modifiedDate", out var modified) && modified.ValueKind == JsonValueKind.String)
            item.ModifiedDate = modified.TryGetDateTime(out var dt2) ? dt2 : null;

        return item;
    }

    private static string ResolveSubscriptionStatus(JsonElement el)
    {
        // Prefer explicit approver decisions when present; these represent the
        // authoritative approval outcome even when top-level subscriptionStatus
        // remains Pending during backend processing.
        if (el.TryGetProperty("policySetValues", out var policySetValues) &&
            policySetValues.TryGetProperty("approverDecisions", out var approverDecisions) &&
            approverDecisions.ValueKind == JsonValueKind.Array &&
            approverDecisions.GetArrayLength() > 0)
        {
            var decisions = approverDecisions
                .EnumerateArray()
                .Select(static entry =>
                    entry.TryGetProperty("decision", out var decision) && decision.ValueKind == JsonValueKind.String
                        ? decision.GetString()
                        : null)
                .Where(static decision => !string.IsNullOrWhiteSpace(decision))
                .ToList();

            if (decisions.Any(static decision =>
                    string.Equals(decision, "Approved", StringComparison.OrdinalIgnoreCase)))
                return "Approved";

            if (decisions.Any(static decision =>
                    string.Equals(decision, "Rejected", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(decision, "Denied", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(decision, "Declined", StringComparison.OrdinalIgnoreCase)))
                return "Denied";

            if (decisions.All(static decision =>
                    string.Equals(decision, "NoResponse", StringComparison.OrdinalIgnoreCase)))
                return "Pending";
        }

        if (el.TryGetProperty("subscriptionStatus", out var subscriptionStatus) &&
            subscriptionStatus.ValueKind == JsonValueKind.String)
        {
            var normalized = NormalizeSubscriptionStatus(subscriptionStatus.GetString());
            if (normalized != null)
                return normalized;
        }

        if (el.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String)
            return NormalizeSubscriptionStatus(status.GetString()) ?? "Pending";

        return "Pending";
    }

    private static string? NormalizeSubscriptionStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToLowerInvariant() switch
        {
            "pending" => "Pending",
            "inreview" => "UnderReview",
            "underreview" => "UnderReview",
            "review" => "UnderReview",
            "active" => "Approved",
            "approved" => "Approved",
            "denied" => "Denied",
            "rejected" => "Denied",
            "cancelled" => "Cancelled",
            "canceled" => "Cancelled",
            // These values appear to describe the underlying workflow/provisioning state,
            // not the end-user approval state, so keep them as Pending in the UI.
            "completed" => "Pending",
            "declined" => "Pending",
            "inprogress" => "Pending",
            "notstarted" => "Pending",
            "noresponse" => "Pending",
            _ => null
        };
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
