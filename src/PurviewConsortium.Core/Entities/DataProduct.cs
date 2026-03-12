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
    public string? OwnerContactsJson { get; set; }
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
    public string? UseCases { get; set; }
    public int? DataQualityScore { get; set; }
    public string? TermsOfUseUrl { get; set; }
    public string? TermsOfUseJson { get; set; }
    public string? DocumentationUrl { get; set; }
    public string? DocumentationJson { get; set; }
    public string? DataAssetsJson { get; set; }

    // Fabric integration — the lakehouse item that IS this data product's source asset
    public string? SourceLakehouseItemId { get; set; }

    // Navigation properties
    public Institution Institution { get; set; } = null!;
    public ICollection<AccessRequest> AccessRequests { get; set; } = new List<AccessRequest>();
    public ICollection<DataProductDataAsset> DataProductDataAssets { get; set; } = new List<DataProductDataAsset>();

    // Helper methods for JSON arrays
    public List<string> GetClassifications() =>
        string.IsNullOrEmpty(ClassificationsJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(ClassificationsJson) ?? new List<string>();

    public List<string> GetGlossaryTerms() =>
        string.IsNullOrEmpty(GlossaryTermsJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(GlossaryTermsJson) ?? new List<string>();

    public List<DataProductOwnerContactInfo> GetOwnerContacts() =>
        string.IsNullOrEmpty(OwnerContactsJson)
            ? new List<DataProductOwnerContactInfo>()
            : System.Text.Json.JsonSerializer.Deserialize<List<DataProductOwnerContactInfo>>(OwnerContactsJson) ?? new List<DataProductOwnerContactInfo>();

    public List<DataProductLinkInfo> GetTermsOfUseLinks() =>
        string.IsNullOrEmpty(TermsOfUseJson)
            ? new List<DataProductLinkInfo>()
            : System.Text.Json.JsonSerializer.Deserialize<List<DataProductLinkInfo>>(TermsOfUseJson) ?? new List<DataProductLinkInfo>();

    public List<DataProductLinkInfo> GetDocumentationLinks() =>
        string.IsNullOrEmpty(DocumentationJson)
            ? new List<DataProductLinkInfo>()
            : System.Text.Json.JsonSerializer.Deserialize<List<DataProductLinkInfo>>(DocumentationJson) ?? new List<DataProductLinkInfo>();

    public List<DataAssetInfo> GetDataAssets() =>
        string.IsNullOrEmpty(DataAssetsJson)
            ? new List<DataAssetInfo>()
            : System.Text.Json.JsonSerializer.Deserialize<List<DataAssetInfo>>(DataAssetsJson) ?? new List<DataAssetInfo>();
}

public class DataProductOwnerContactInfo
{
    public string? Id { get; set; }
    public string? Description { get; set; }
    public string? Name { get; set; }
    public string? EmailAddress { get; set; }
}

public class DataProductLinkInfo
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? DataAssetId { get; set; }
}

public class DataAssetInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Description { get; set; }
}
