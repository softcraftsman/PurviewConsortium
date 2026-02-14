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
}

public interface IPurviewScannerService
{
    Task<List<DataProductSyncResult>> ScanForShareableDataProductsAsync(
        string purviewAccountName,
        string tenantId,
        CancellationToken cancellationToken = default);
}
