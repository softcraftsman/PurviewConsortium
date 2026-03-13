namespace PurviewConsortium.Core.Interfaces;

using PurviewConsortium.Core.Entities;

public class DataProductSyncResult
{
    public string PurviewQualifiedName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Owner { get; set; }
    public string? OwnerEmail { get; set; }
    /// <summary>The Azure AD Object ID of the owner (GUID from contacts.owner), used for Graph API resolution.</summary>
    public string? OwnerObjectId { get; set; }
    public List<DataProductOwnerContactInfo> OwnerContacts { get; set; } = new();
    public string? SourceSystem { get; set; }
    public string? SchemaJson { get; set; }
    public List<string> Classifications { get; set; } = new();
    public List<string> GlossaryTerms { get; set; } = new();
    public string? SensitivityLabel { get; set; }
    public DateTime? PurviewLastModified { get; set; }

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
    public List<DataProductLinkInfo> TermsOfUseLinks { get; set; } = new();
    public string? DocumentationUrl { get; set; }
    public List<DataProductLinkInfo> DocumentationLinks { get; set; } = new();
    public List<DataAssetSyncInfo> DataAssets { get; set; } = new();

    /// <summary>Purview data asset IDs referenced by this data product (from termsOfUse/documentation).</summary>
    public List<string> LinkedPurviewAssetIds { get; set; } = new();

    /// <summary>Purview data asset IDs from the termsOfUse array specifically.</summary>
    public List<string> TermsOfUseAssetIds { get; set; } = new();

    /// <summary>Purview data asset IDs from the documentation array specifically.</summary>
    public List<string> DocumentationAssetIds { get; set; } = new();
}

public class DataAssetSyncInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Result of scanning a single Purview Data Asset from /datagovernance/catalog/dataAssets.
/// </summary>
public class DataAssetSyncResult
{
    public string PurviewAssetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? AssetType { get; set; }
    public string? FullyQualifiedName { get; set; }
    public string? AccountName { get; set; }
    public string? WorkspaceName { get; set; }
    public string? SourceWorkspaceId { get; set; }
    public string? ProvisioningState { get; set; }
    public DateTime? LastRefreshedAt { get; set; }
    public DateTime? PurviewCreatedAt { get; set; }
    public DateTime? PurviewLastModifiedAt { get; set; }
    public string? ContactsJson { get; set; }
    public string? ClassificationsJson { get; set; }
}

public interface IPurviewScannerService
{
    /// <summary>
    /// Scans Purview Unified Catalog for Data Products.
    /// Uses client credentials (service principal) to authenticate with Purview.
    /// When <paramref name="consortiumDomainIds"/> is provided, only Data Products in those
    /// governance domains are returned.
    /// </summary>
    Task<List<DataProductSyncResult>> ScanForShareableDataProductsAsync(
        string purviewAccountName,
        string tenantId,
        string? consortiumDomainIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all Data Assets from the Purview Unified Catalog.
    /// </summary>
    Task<List<DataAssetSyncResult>> ScanForDataAssetsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the detail for a single data product from Purview and extracts linked data asset IDs
    /// from termsOfUse and documentation arrays.
    /// </summary>
    Task<List<string>> FetchProductLinkedAssetIdsAsync(
        string purviewProductId,
        string tenantId,
        CancellationToken cancellationToken = default);
}
