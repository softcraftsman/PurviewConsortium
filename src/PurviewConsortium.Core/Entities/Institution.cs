namespace PurviewConsortium.Core.Entities;

public class Institution
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string PurviewAccountName { get; set; } = string.Empty;
    public string? FabricWorkspaceId { get; set; }
    /// <summary>
    /// Comma-separated list of Purview governance domain IDs to sync.
    /// Only Data Products in these domains will be imported.
    /// If empty/null, all Data Products are synced.
    /// </summary>
    public string? ConsortiumDomainIds { get; set; }
    public string PrimaryContactEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool AdminConsentGranted { get; set; }
    /// <summary>
    /// When true (and the global Fabric:AutoFulfillOnApproval setting is also true),
    /// the system will attempt to automatically create Fabric shortcuts or external data
    /// shares as the Application Identity upon request approval.
    /// Requires FabricWorkspaceId on this institution and SourceLakehouseItemId on the
    /// Data Product to be configured.
    /// </summary>
    public bool AutoFulfillEnabled { get; set; } = false;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<DataProduct> DataProducts { get; set; } = new List<DataProduct>();
    public ICollection<AccessRequest> IncomingRequests { get; set; } = new List<AccessRequest>();
    public ICollection<SyncHistory> SyncHistories { get; set; } = new List<SyncHistory>();
}
