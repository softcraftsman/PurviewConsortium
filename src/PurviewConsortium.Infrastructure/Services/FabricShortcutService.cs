using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Infrastructure.Services;

/// <summary>
/// Creates cross-tenant Fabric OneLake shortcuts via the Fabric REST API.
///
/// Phase 2 automated fulfillment flow:
///   1. POST external data share on the source workspace item.
///   2. POST a OneLake shortcut in the consumer's target lakehouse referencing the share.
///
/// Fabric REST API references:
///   - External Data Shares: POST /v1/workspaces/{workspaceId}/items/{itemId}/externalDataShares
///   - Shortcuts:            POST /v1/workspaces/{workspaceId}/items/{itemId}/shortcuts
///   - Revoke Share:         DELETE /v1/workspaces/{workspaceId}/items/{itemId}/externalDataShares/{shareId}
/// </summary>
public class FabricShortcutService : IFabricShortcutService
{
    private const string FabricScope = "https://api.fabric.microsoft.com/.default";
    private const string FabricBaseUrl = "https://api.fabric.microsoft.com/v1";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FabricShortcutService> _logger;

    public FabricShortcutService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FabricShortcutService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AutoFulfillmentResult> CreateCrossTenantShortcutAsync(
        string sourceWorkspaceId,
        string sourceItemId,
        string sourceTenantId,
        string recipientTenantId,
        string recipientUserEmail,
        string targetWorkspaceId,
        string targetLakehouseId,
        string dataProductName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting automated cross-tenant shortcut creation for Data Product '{Name}'. " +
            "Source lakehouse: workspace={SourceWs}, item={SourceItem}, tenant={SourceTenant}. " +
            "Target lakehouse: workspace={TargetWs}, item={TargetLh}, tenant={TargetTenant}",
            dataProductName, sourceWorkspaceId, sourceItemId, sourceTenantId,
            targetWorkspaceId, targetLakehouseId, recipientTenantId);

        try
        {
            // Step 1: Create external data share in the source institution's workspace
            var shareResult = await CreateExternalDataShareAsync(
                sourceWorkspaceId, sourceItemId, sourceTenantId,
                recipientTenantId, recipientUserEmail,
                cancellationToken);

            if (!shareResult.Success)
            {
                _logger.LogError(
                    "Failed to create external data share for '{Name}': {Error}",
                    dataProductName, shareResult.ErrorMessage);

                return new AutoFulfillmentResult
                {
                    Success = false,
                    ErrorMessage = $"External data share creation failed: {shareResult.ErrorMessage}"
                };
            }

            _logger.LogInformation(
                "External data share created: ShareId={ShareId} for Data Product '{Name}'",
                shareResult.ExternalShareId, dataProductName);

            // Step 2: Create OneLake shortcut in the consumer's lakehouse
            var shortcutResult = await CreateOneLakeShortcutAsync(
                targetWorkspaceId, targetLakehouseId, recipientTenantId,
                sourceWorkspaceId, sourceItemId,
                shareResult.ExternalShareId!,
                dataProductName,
                cancellationToken);

            if (!shortcutResult.Success)
            {
                _logger.LogWarning(
                    "External share created (ShareId={ShareId}) but shortcut creation failed for '{Name}': {Error}. " +
                    "The consumer may need to create the shortcut manually.",
                    shareResult.ExternalShareId, dataProductName, shortcutResult.ErrorMessage);

                return new AutoFulfillmentResult
                {
                    Success = false,
                    PartialSuccess = true,
                    ExternalShareId = shareResult.ExternalShareId,
                    ErrorMessage = $"Share created but shortcut creation failed: {shortcutResult.ErrorMessage}"
                };
            }

            _logger.LogInformation(
                "Cross-tenant shortcut created successfully for '{Name}'. " +
                "ShareId={ShareId}, ShortcutName={ShortcutName}",
                dataProductName, shareResult.ExternalShareId, shortcutResult.ShortcutName);

            return new AutoFulfillmentResult
            {
                Success = true,
                ExternalShareId = shareResult.ExternalShareId,
                ShortcutName = shortcutResult.ShortcutName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during automated shortcut creation for '{Name}'",
                dataProductName);

            return new AutoFulfillmentResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> RevokeExternalShareAsync(
        string sourceWorkspaceId,
        string sourceItemId,
        string externalShareId,
        string sourceTenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Revoking external data share {ShareId} from workspace={WorkspaceId}, item={ItemId}",
            externalShareId, sourceWorkspaceId, sourceItemId);

        try
        {
            var accessToken = await GetFabricTokenAsync(sourceTenantId, cancellationToken);
            var httpClient = _httpClientFactory.CreateClient("Fabric");

            var url = $"{FabricBaseUrl}/workspaces/{sourceWorkspaceId}/items/{sourceItemId}" +
                      $"/externalDataShares/{externalShareId}";

            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("External data share {ShareId} revoked successfully", externalShareId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to revoke external data share {ShareId}: {Status} - {Error}",
                externalShareId, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking external data share {ShareId}", externalShareId);
            return false;
        }
    }

    /// <summary>
    /// Creates an external data share in the source institution's Fabric workspace.
    /// POST /v1/workspaces/{workspaceId}/items/{itemId}/externalDataShares
    /// </summary>
    private async Task<ExternalDataShareResult> CreateExternalDataShareAsync(
        string sourceWorkspaceId,
        string sourceItemId,
        string sourceTenantId,
        string recipientTenantId,
        string recipientUserEmail,
        CancellationToken cancellationToken)
    {
        var accessToken = await GetFabricTokenAsync(sourceTenantId, cancellationToken);
        var httpClient = _httpClientFactory.CreateClient("Fabric");

        var url = $"{FabricBaseUrl}/workspaces/{sourceWorkspaceId}/items/{sourceItemId}/externalDataShares";

        var payload = new
        {
            paths = new[]
            {
                new { path = "/" }   // Share the entire item (root path)
            },
            recipient = new
            {
                tenantId = recipientTenantId,
                userPrincipalName = recipientUserEmail
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Fabric external data share API returned {Status}: {Body}",
                response.StatusCode, responseBody);

            return new ExternalDataShareResult
            {
                Success = false,
                ErrorMessage = $"Fabric API error ({response.StatusCode}): {responseBody}"
            };
        }

        using var doc = JsonDocument.Parse(responseBody);
        var shareId = doc.RootElement.TryGetProperty("id", out var idVal)
            ? idVal.GetString() : null;

        return new ExternalDataShareResult
        {
            Success = true,
            ExternalShareId = shareId
        };
    }

    /// <summary>
    /// Creates a OneLake shortcut in the consumer's target lakehouse that references the external data share.
    /// POST /v1/workspaces/{workspaceId}/items/{lakehouseId}/shortcuts
    /// </summary>
    private async Task<ShortcutCreationResult> CreateOneLakeShortcutAsync(
        string targetWorkspaceId,
        string targetLakehouseId,
        string recipientTenantId,
        string sourceWorkspaceId,
        string sourceItemId,
        string externalShareId,
        string dataProductName,
        CancellationToken cancellationToken)
    {
        var accessToken = await GetFabricTokenAsync(recipientTenantId, cancellationToken);
        var httpClient = _httpClientFactory.CreateClient("Fabric");

        var url = $"{FabricBaseUrl}/workspaces/{targetWorkspaceId}/items/{targetLakehouseId}/shortcuts";

        // Sanitize name for use as a shortcut path
        var shortcutName = SanitizeShortcutName(dataProductName);
        var shortcutPath = $"Tables/{shortcutName}";

        var payload = new
        {
            name = shortcutName,
            path = shortcutPath,
            target = new
            {
                oneLake = new
                {
                    workspaceId = sourceWorkspaceId,
                    itemId = sourceItemId,
                    path = "/"
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Fabric shortcut API returned {Status}: {Body}",
                response.StatusCode, responseBody);

            return new ShortcutCreationResult
            {
                Success = false,
                ErrorMessage = $"Fabric API error ({response.StatusCode}): {responseBody}"
            };
        }

        return new ShortcutCreationResult
        {
            Success = true,
            ShortcutName = shortcutName,
            ShortcutPath = shortcutPath
        };
    }

    /// <summary>
    /// Acquires a Fabric access token for the given tenant via client credentials.
    /// </summary>
    private async Task<string> GetFabricTokenAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var clientId = _configuration["AzureAd:ClientId"];
        var clientSecret = _configuration["AzureAd:ClientSecret"];

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var tokenResult = await credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { FabricScope }),
            cancellationToken);

        return tokenResult.Token;
    }

    /// <summary>
    /// Sanitizes a data product name for use as a OneLake shortcut name.
    /// Removes special characters and replaces spaces with underscores.
    /// </summary>
    private static string SanitizeShortcutName(string name)
    {
        // Replace spaces and hyphens with underscores
        var sanitized = Regex.Replace(name, @"[\s\-]+", "_");
        // Remove any characters that aren't alphanumeric or underscore
        sanitized = Regex.Replace(sanitized, @"[^\w]", "");
        // Trim to 128 characters (Fabric shortcut name limit)
        if (sanitized.Length > 128)
            sanitized = sanitized[..128];
        return sanitized;
    }
}
