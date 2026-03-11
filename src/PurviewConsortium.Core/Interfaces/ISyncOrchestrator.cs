namespace PurviewConsortium.Core.Interfaces;

public interface ISyncOrchestrator
{
    Task ScanAllInstitutionsAsync(CancellationToken cancellationToken = default);
    Task ScanInstitutionAsync(Guid institutionId, CancellationToken cancellationToken = default);
}
