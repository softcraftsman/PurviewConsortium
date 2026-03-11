namespace PurviewConsortium.Core.Entities;

/// <summary>
/// Represents a Data Asset from the Purview Unified Catalog.
/// Fetched from /datagovernance/catalog/dataAssets endpoint.
/// </summary>
public class DataAsset
{
    public Guid Id { get; set; }

    /// <summary>The Purview-assigned ID for this data asset.</summary>
    public string PurviewAssetId { get; set; } = string.Empty;

    /// <summary>Institution that owns the Purview account this asset belongs to.</summary>
    public Guid InstitutionId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Purview classification type, e.g. "General".</summary>
    public string? Type { get; set; }

    public string? Description { get; set; }

    /// <summary>Source asset type, e.g. "fabric_lakehouse", "powerbi_dataset", "azure_blob_container".</summary>
    public string? AssetType { get; set; }

    /// <summary>Fully qualified name / URL of the source asset.</summary>
    public string? FullyQualifiedName { get; set; }

    /// <summary>Purview account name from source.accountName.</summary>
    public string? AccountName { get; set; }

    /// <summary>Fabric workspace name if applicable.</summary>
    public string? WorkspaceName { get; set; }

    /// <summary>Provisioning state: Succeeded, SoftDeleted, etc.</summary>
    public string? ProvisioningState { get; set; }

    /// <summary>When the asset was last refreshed in the data map.</summary>
    public DateTime? LastRefreshedAt { get; set; }

    /// <summary>When the asset was created in Purview.</summary>
    public DateTime? PurviewCreatedAt { get; set; }

    /// <summary>When the asset was last modified in Purview.</summary>
    public DateTime? PurviewLastModifiedAt { get; set; }

    /// <summary>JSON-serialized contacts object from Purview.</summary>
    public string? ContactsJson { get; set; }

    /// <summary>JSON-serialized classifications array.</summary>
    public string? ClassificationsJson { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Institution Institution { get; set; } = null!;
    public ICollection<DataProductDataAsset> DataProductDataAssets { get; set; } = new List<DataProductDataAsset>();
}
