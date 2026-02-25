namespace PurviewConsortium.Core.Interfaces;

/// <summary>
/// Result of creating an external data share in the source Fabric workspace.
/// </summary>
public class ExternalDataShareResult
{
    public bool Success { get; set; }
    public string? ExternalShareId { get; set; }
    public string? RecipientShareUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of creating a OneLake shortcut in the consumer's Fabric lakehouse.
/// </summary>
public class ShortcutCreationResult
{
    public bool Success { get; set; }
    public string? ShortcutName { get; set; }
    public string? ShortcutPath { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of the full automated fulfillment flow (external share + shortcut).
/// </summary>
public class AutoFulfillmentResult
{
    public bool Success { get; set; }
    public string? ExternalShareId { get; set; }
    public string? ShortcutName { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Whether the share was created but the shortcut step failed.</summary>
    public bool PartialSuccess { get; set; }
}

/// <summary>
/// Creates cross-tenant Fabric OneLake shortcuts via the Fabric REST API.
///
/// Flow:
/// 1. Create an external data share in the source institution's Fabric workspace.
/// 2. Accept the share and create a OneLake shortcut in the consumer's Fabric lakehouse.
/// 3. Optionally revoke an existing share.
/// </summary>
public interface IFabricShortcutService
{
    /// <summary>
    /// Creates an external data share from the source institution's workspace item,
    /// then creates a OneLake shortcut in the consumer's target lakehouse.
    /// </summary>
    /// <param name="sourceWorkspaceId">Source institution's Fabric workspace ID.</param>
    /// <param name="sourceItemId">The item ID (e.g. lakehouse) in the source workspace to share.</param>
    /// <param name="sourceWorkspaceId">Source institution's Fabric workspace containing the lakehouse.</param>
    /// <param name="sourceItemId">Source lakehouse item ID (the Data Product's Purview Data Asset).</param>
    /// <param name="sourceTenantId">Source institution's Azure AD tenant ID.</param>
    /// <param name="recipientTenantId">Consumer institution's Azure AD tenant ID.</param>
    /// <param name="recipientUserEmail">Consumer user's email for share recipient.</param>
    /// <param name="targetWorkspaceId">Consumer's target Fabric workspace ID.</param>
    /// <param name="targetLakehouseId">Consumer's target lakehouse item ID.</param>
    /// <param name="dataProductName">Name of the data product (used for shortcut naming).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AutoFulfillmentResult> CreateCrossTenantShortcutAsync(
        string sourceWorkspaceId,
        string sourceItemId,
        string sourceTenantId,
        string recipientTenantId,
        string recipientUserEmail,
        string targetWorkspaceId,
        string targetLakehouseId,
        string dataProductName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an existing external data share, invalidating the consumer's shortcut.
    /// </summary>
    Task<bool> RevokeExternalShareAsync(
        string sourceWorkspaceId,
        string sourceItemId,
        string externalShareId,
        string sourceTenantId,
        CancellationToken cancellationToken = default);
}
