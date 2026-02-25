using PurviewConsortium.Core.Enums;

namespace PurviewConsortium.Core.Entities;

public class AccessRequest
{
    public Guid Id { get; set; }
    public Guid DataProductId { get; set; }
    public string RequestingUserId { get; set; } = string.Empty;
    public string RequestingUserEmail { get; set; } = string.Empty;
    public string RequestingUserName { get; set; } = string.Empty;
    public Guid? RequestingInstitutionId { get; set; }
    public string? RequestingTenantId { get; set; }
    public string? TargetFabricWorkspaceId { get; set; }
    public string? TargetLakehouseItemId { get; set; }
    public string BusinessJustification { get; set; } = string.Empty;
    public int? RequestedDurationDays { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Submitted;
    public DateTime? StatusChangedDate { get; set; }
    public string? StatusChangedBy { get; set; }
    public string? ExternalShareId { get; set; }
    public string? FabricShortcutName { get; set; }
    public bool FabricShortcutCreated { get; set; }
    public string? PurviewWorkflowRunId { get; set; }
    public string? PurviewWorkflowStatus { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public DataProduct DataProduct { get; set; } = null!;
    public Institution? RequestingInstitution { get; set; }
}
