namespace PurviewConsortium.Core.Interfaces;

public interface ISyncOrchestrator
{
    Task ScanAllInstitutionsAsync(string? userAccessToken = null, CancellationToken cancellationToken = default);
    Task ScanInstitutionAsync(Guid institutionId, string? userAccessToken = null, CancellationToken cancellationToken = default);
}
