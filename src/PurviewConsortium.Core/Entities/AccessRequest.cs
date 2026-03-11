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

    // --- Denormalized source details (captured at request creation time) ---
    // These are snapshotted so the fulfillment view remains accurate even if
    // the institution record or data product is later updated.

    /// <summary>Whether this is a same-tenant (Internal) or cross-tenant (External) share.</summary>
    public ShareType ShareType { get; set; } = ShareType.External;

    /// <summary>Source institution's Fabric workspace ID (from Institution.FabricWorkspaceId at request time).</summary>
    public string? SourceFabricWorkspaceId { get; set; }

    /// <summary>Source data product's lakehouse item ID (from DataProduct.SourceLakehouseItemId at request time).</summary>
    public string? SourceLakehouseItemId { get; set; }

    /// <summary>Source institution's Azure AD tenant ID (from Institution.TenantId at request time).</summary>
    public string? SourceTenantId { get; set; }

    /// <summary>Source institution's display name (from Institution.Name at request time).</summary>
    public string? SourceInstitutionName { get; set; }

    // Navigation properties
    public DataProduct DataProduct { get; set; } = null!;
    public Institution? RequestingInstitution { get; set; }
}
