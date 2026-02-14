using PurviewConsortium.Core.Enums;

namespace PurviewConsortium.Core.Entities;

public class SyncHistory
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public SyncStatus Status { get; set; }
    public int ProductsFound { get; set; }
    public int ProductsAdded { get; set; }
    public int ProductsUpdated { get; set; }
    public int ProductsDelisted { get; set; }
    public string? ErrorDetails { get; set; }

    // Navigation properties
    public Institution Institution { get; set; } = null!;
}
