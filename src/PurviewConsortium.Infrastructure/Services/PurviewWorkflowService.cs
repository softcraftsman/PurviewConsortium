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
/// Submits self-service data access requests to the Purview Workflow API.
/// 
/// Flow:
/// 1. Acquires a Purview token via OBO (or client credentials fallback).
/// 2. Searches the DataMap for an asset matching the Data Product name.
/// 3. Submits a GrantDataAccess user request to trigger the configured workflow.
/// </summary>
public class PurviewWorkflowService : IPurviewWorkflowService
{
    private const string PurviewScope = "https://purview.azure.net/.default";
    private const string WorkflowApiVersion = "2022-05-01-preview";
    private const string DataMapApiVersion = "2023-09-01";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PurviewWorkflowService> _logger;

    public PurviewWorkflowService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PurviewWorkflowService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<WorkflowSubmitResult> SubmitAccessRequestAsync(
        string purviewAccountName,
        string tenantId,
        string dataProductName,
        string businessJustification,
        string? userAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Submitting Purview workflow access request for Data Product '{Name}' on account '{Account}'",
            dataProductName, purviewAccountName);

        try
        {
            var accessToken = await GetAccessTokenAsync(tenantId, userAccessToken, cancellationToken);
            var httpClient = _httpClientFactory.CreateClient("Purview");
            var purviewEndpoint = $"https://{purviewAccountName}.purview.azure.com";

            // Step 1: Search DataMap for the asset by name to get its GUID
            var assetGuid = await FindDataMapAssetGuidAsync(
                httpClient, accessToken, purviewEndpoint, dataProductName, cancellationToken);

            if (string.IsNullOrEmpty(assetGuid))
            {
                _logger.LogWarning(
                    "No DataMap asset found matching Data Product name '{Name}'. " +
                    "Workflow request cannot be submitted without a valid DataMap asset GUID.",
                    dataProductName);

                return new WorkflowSubmitResult
                {
                    Success = false,
                    ErrorMessage = $"No matching DataMap asset found for '{dataProductName}'. " +
                                   "The Data Product may not have registered assets in the Purview DataMap."
                };
            }

            _logger.LogInformation("Found DataMap asset GUID {Guid} for Data Product '{Name}'",
                assetGuid, dataProductName);

            // Step 2: Submit the GrantDataAccess workflow request
            var workflowRunId = await SubmitWorkflowRequestAsync(
                httpClient, accessToken, purviewEndpoint,
                assetGuid, businessJustification, cancellationToken);

            _logger.LogInformation(
                "Purview workflow request submitted successfully. WorkflowRunId: {RunId}",
                workflowRunId);

            return new WorkflowSubmitResult
            {
                Success = true,
                WorkflowRunId = workflowRunId,
                DataMapAssetGuid = assetGuid
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to submit Purview workflow request for Data Product '{Name}'",
                dataProductName);

            return new WorkflowSubmitResult
            {
                Success = false,
                ErrorMessage = $"Purview workflow submission failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Searches the Purview DataMap for an asset matching the given name.
    /// Returns the GUID of the first match, or null if none found.
    /// </summary>
    private async Task<string?> FindDataMapAssetGuidAsync(
        HttpClient httpClient,
        string accessToken,
        string purviewEndpoint,
        string dataProductName,
        CancellationToken cancellationToken)
    {
        var searchUrl = $"{purviewEndpoint}/datamap/api/search/query?api-version={DataMapApiVersion}";
        var searchBody = new { keywords = dataProductName, limit = 5 };

        var request = new HttpRequestMessage(HttpMethod.Post, searchUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(searchBody),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("DataMap search returned {Status}: {Error}",
                response.StatusCode, errorBody);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("value", out var results))
            return null;

        // Find the best match — prefer exact name match
        foreach (var result in results.EnumerateArray())
        {
            var name = result.TryGetProperty("name", out var nameVal)
                ? nameVal.GetString() : null;
            var id = result.TryGetProperty("id", out var idVal)
                ? idVal.GetString() : null;

            if (name != null && id != null &&
                name.Equals(dataProductName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Exact name match in DataMap: '{Name}' → {Guid}", name, id);
                return id;
            }
        }

        // Fallback: return first result's GUID if any
        if (results.GetArrayLength() > 0)
        {
            var first = results[0];
            var id = first.TryGetProperty("id", out var idVal) ? idVal.GetString() : null;
            var name = first.TryGetProperty("name", out var nameVal) ? nameVal.GetString() : null;
            _logger.LogDebug("Using first DataMap search result: '{Name}' → {Guid}", name, id);
            return id;
        }

        return null;
    }

    /// <summary>
    /// Submits a GrantDataAccess user request to the Purview Workflow API.
    /// The payload uses flat field names: dataAssetGuid, note, purviewDataRole.
    /// </summary>
    private async Task<string> SubmitWorkflowRequestAsync(
        HttpClient httpClient,
        string accessToken,
        string purviewEndpoint,
        string dataAssetGuid,
        string businessJustification,
        CancellationToken cancellationToken)
    {
        var workflowUrl = $"{purviewEndpoint}/workflow/userrequests?api-version={WorkflowApiVersion}";

        var payload = new
        {
            operations = new[]
            {
                new
                {
                    // TODO: What other payload items could I provide here. 
                    type = "GrantDataAccess",
                    payload = new Dictionary<string, object>
                    {
                        ["note"] = businessJustification,
                        ["purviewDataRole"] = "DataReader",
                        ["dataAssetGuid"] = dataAssetGuid
                    }
                }
            },
            comment = $"Access request submitted via Purview Consortium platform"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, workflowUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Purview Workflow API returned {Status}: {Body}",
                response.StatusCode, responseBody);
            throw new InvalidOperationException(
                $"Purview Workflow API error ({response.StatusCode}): {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var requestId = doc.RootElement.TryGetProperty("requestId", out var ridVal)
            ? ridVal.GetString()
            : null;

        // The workflow run ID is in operations[0].workflowRunIds[0]
        string? workflowRunId = null;
        if (doc.RootElement.TryGetProperty("operations", out var ops) &&
            ops.GetArrayLength() > 0 &&
            ops[0].TryGetProperty("workflowRunIds", out var runIds) &&
            runIds.GetArrayLength() > 0)
        {
            workflowRunId = runIds[0].GetString();
        }

        return workflowRunId ?? requestId ?? "unknown";
    }

    public async Task<WorkflowRunStatusResult> GetWorkflowRunStatusAsync(
        string purviewAccountName,
        string tenantId,
        string workflowRunId,
        string? userAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync(tenantId, userAccessToken, cancellationToken);
            var httpClient = _httpClientFactory.CreateClient("Purview");
            var purviewEndpoint = $"https://{purviewAccountName}.purview.azure.com";

            // GET workflow run status
            var url = $"{purviewEndpoint}/workflow/workflowruns/{workflowRunId}?api-version={WorkflowApiVersion}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Workflow run status check returned {Status}: {Body}",
                    response.StatusCode, body);
                return new WorkflowRunStatusResult
                {
                    Success = false,
                    ErrorMessage = $"API returned {response.StatusCode}"
                };
            }

            _logger.LogDebug(
                "Raw Purview workflow run response for {RunId}: {Body}",
                workflowRunId, body);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var runStatus = root.TryGetProperty("status", out var statusVal)
                ? SafeGetString(statusVal) : null;

            // Extract approval outcome from the Purview Workflow API response.
            // The response structure uses "actions" (not "actionDetails") with named keys like
            // "Start and wait for an approval". The outcome is at: actions.{key}.output.body.outcome
            string? approvalOutcome = null;

            // Strategy 1: Check "actions" object (actual Purview Workflow API structure)
            if (root.TryGetProperty("actions", out var actionsObj))
            {
                foreach (var action in actionsObj.EnumerateObject())
                {
                    var actionObj = action.Value;

                    // Match by action name containing "approval" (e.g. "Start and wait for an approval")
                    bool isApprovalAction = action.Name.Contains("approval", StringComparison.OrdinalIgnoreCase);

                    // Also match by type if present
                    if (!isApprovalAction && actionObj.TryGetProperty("type", out var typeVal))
                    {
                        var actionType = SafeGetString(typeVal);
                        isApprovalAction = string.Equals(actionType, "Approval", StringComparison.OrdinalIgnoreCase);
                    }

                    if (isApprovalAction)
                    {
                        // Primary path: output.body.outcome (confirmed from live API response)
                        if (actionObj.TryGetProperty("output", out var outputObj) &&
                            outputObj.TryGetProperty("body", out var bodyObj) &&
                            bodyObj.TryGetProperty("outcome", out var outcomeVal))
                        {
                            approvalOutcome = SafeGetString(outcomeVal);
                        }

                        // Fallback paths within the action object
                        if (approvalOutcome == null && actionObj.TryGetProperty("outcome", out var directOutcome))
                            approvalOutcome = SafeGetString(directOutcome);
                        if (approvalOutcome == null && actionObj.TryGetProperty("result", out var resultVal2))
                            approvalOutcome = SafeGetString(resultVal2);
                        if (approvalOutcome == null && actionObj.TryGetProperty("status", out var statusVal2) &&
                            statusVal2.ValueKind == JsonValueKind.String)
                            approvalOutcome = statusVal2.GetString();

                        if (approvalOutcome != null) break;
                    }
                }
            }

            // Strategy 2: Legacy "actionDetails" structure (in case some Purview versions use it)
            if (approvalOutcome == null && root.TryGetProperty("actionDetails", out var actionDetails))
            {
                foreach (var action in actionDetails.EnumerateObject())
                {
                    var actionObj = action.Value;
                    var actionType = actionObj.TryGetProperty("type", out var typeVal)
                        ? SafeGetString(typeVal) : null;

                    if (string.Equals(actionType, "Approval", StringComparison.OrdinalIgnoreCase))
                    {
                        if (actionObj.TryGetProperty("output", out var outputObj) &&
                            outputObj.TryGetProperty("body", out var bodyObj) &&
                            bodyObj.TryGetProperty("outcome", out var outcomeVal))
                        {
                            approvalOutcome = SafeGetString(outcomeVal);
                        }
                        if (approvalOutcome == null && actionObj.TryGetProperty("status", out var s) &&
                            s.ValueKind == JsonValueKind.String)
                            approvalOutcome = s.GetString();
                        if (approvalOutcome == null && actionObj.TryGetProperty("result", out var r) &&
                            r.ValueKind == JsonValueKind.String)
                            approvalOutcome = r.GetString();

                        if (approvalOutcome != null) break;
                    }
                }
            }

            // Strategy 3: Check top-level "result" only if it's a simple string (not a complex object)
            if (approvalOutcome == null && root.TryGetProperty("result", out var topResult) &&
                topResult.ValueKind == JsonValueKind.String)
            {
                approvalOutcome = topResult.GetString();
            }

            _logger.LogInformation(
                "Workflow run {RunId} status: {Status}, approvalOutcome: {Outcome}",
                workflowRunId, runStatus, approvalOutcome ?? "(none)");

            return new WorkflowRunStatusResult
            {
                Success = true,
                RunStatus = runStatus,
                ApprovalOutcome = approvalOutcome
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Failed to check workflow run status for {RunId} on account {Account} " +
                "(tenant {TenantId}): [{ExceptionType}] {Message}",
                workflowRunId, purviewAccountName, tenantId,
                ex.GetType().Name, ex.Message);
            return new WorkflowRunStatusResult
            {
                Success = false,
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Safely extract a string from a JsonElement. If the element is an object or array,
    /// returns its JSON representation instead of throwing InvalidOperationException.
    /// </summary>
    private static string? SafeGetString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => element.GetRawText(),
            _ => element.GetRawText() // Object or Array — return raw JSON
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
            _logger.LogDebug("Acquiring Purview token via OBO flow for tenant {TenantId}", tenantId);
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
        else
        {
            _logger.LogDebug(
                "No user token provided for workflow status check. Using client credentials for tenant {TenantId}.",
                tenantId);
        }

        // Fallback: client credentials
        try
        {
            _logger.LogDebug("Acquiring Purview token via client credentials for tenant {TenantId}", tenantId);
            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var tokenResult = await credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(new[] { PurviewScope }),
                cancellationToken);

            return tokenResult.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Client credentials token acquisition ALSO failed for tenant {TenantId} " +
                "({ExceptionType}: {Message}). Ensure the consortium app has admin consent " +
                "in the institution's tenant and has Purview permissions.",
                tenantId, ex.GetType().Name, ex.Message);
            throw;
        }
    }
}
