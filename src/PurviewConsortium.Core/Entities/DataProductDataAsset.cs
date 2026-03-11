namespace PurviewConsortium.Core.Entities;

/// <summary>
/// Many-to-many join entity linking Data Products to Data Assets.
/// Represents the association created during Purview sync or manual admin linking.
/// </summary>
public class DataProductDataAsset
{
    public Guid DataProductId { get; set; }
    public Guid DataAssetId { get; set; }

    // Navigation properties
    public DataProduct DataProduct { get; set; } = null!;
    public DataAsset DataAsset { get; set; } = null!;
}
