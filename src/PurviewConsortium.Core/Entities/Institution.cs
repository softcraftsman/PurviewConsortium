namespace PurviewConsortium.Core.Entities;

public class Institution
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string PurviewAccountName { get; set; } = string.Empty;
    public string? FabricWorkspaceId { get; set; }
    public string PrimaryContactEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool AdminConsentGranted { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<DataProduct> DataProducts { get; set; } = new List<DataProduct>();
    public ICollection<AccessRequest> IncomingRequests { get; set; } = new List<AccessRequest>();
    public ICollection<SyncHistory> SyncHistories { get; set; } = new List<SyncHistory>();
}
