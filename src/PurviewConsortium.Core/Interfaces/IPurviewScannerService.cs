namespace PurviewConsortium.Core.Interfaces;

public class DataProductSyncResult
{
    public string PurviewQualifiedName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Owner { get; set; }
    public string? OwnerEmail { get; set; }
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
}

public interface IPurviewScannerService
{
    /// <summary>
    /// Scans Purview Unified Catalog for Data Products.
    /// When <paramref name="userAccessToken"/> is provided, uses On-Behalf-Of (OBO) flow
    /// to obtain a Purview token with the user's permissions (which can see all governance domains).
    /// Falls back to client credentials when no user token is available.
    /// When <paramref name="consortiumDomainIds"/> is provided, only Data Products in those
    /// governance domains are returned.
    /// </summary>
    Task<List<DataProductSyncResult>> ScanForShareableDataProductsAsync(
        string purviewAccountName,
        string tenantId,
        string? userAccessToken = null,
        string? consortiumDomainIds = null,
        CancellationToken cancellationToken = default);
}
