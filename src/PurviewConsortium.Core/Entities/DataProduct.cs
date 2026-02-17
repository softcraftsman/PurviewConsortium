namespace PurviewConsortium.Core.Entities;

public class DataProduct
{
    public Guid Id { get; set; }
    public string PurviewQualifiedName { get; set; } = string.Empty;
    public Guid InstitutionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Owner { get; set; }
    public string? OwnerEmail { get; set; }
    public string? SourceSystem { get; set; }
    public string? SchemaJson { get; set; }
    public string? ClassificationsJson { get; set; }
    public string? GlossaryTermsJson { get; set; }
    public string? SensitivityLabel { get; set; }
    public bool IsListed { get; set; } = true;
    public DateTime? LastSyncedFromPurview { get; set; }
    public DateTime? PurviewLastModified { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    // Unified Catalog Data Product fields
    public string? Status { get; set; }
    public string? DataProductType { get; set; }
    public string? GovernanceDomain { get; set; }
    public int AssetCount { get; set; }
    public string? BusinessUse { get; set; }
    public bool Endorsed { get; set; }
    public string? UpdateFrequency { get; set; }
    public string? Documentation { get; set; }

    // Navigation properties
    public Institution Institution { get; set; } = null!;
    public ICollection<AccessRequest> AccessRequests { get; set; } = new List<AccessRequest>();

    // Helper methods for JSON arrays
    public List<string> GetClassifications() =>
        string.IsNullOrEmpty(ClassificationsJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(ClassificationsJson) ?? new List<string>();

    public List<string> GetGlossaryTerms() =>
        string.IsNullOrEmpty(GlossaryTermsJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(GlossaryTermsJson) ?? new List<string>();
}
