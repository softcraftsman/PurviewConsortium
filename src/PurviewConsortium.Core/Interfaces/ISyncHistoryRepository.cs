using PurviewConsortium.Core.Entities;

namespace PurviewConsortium.Core.Interfaces;

public interface ISyncHistoryRepository
{
    Task<SyncHistory> CreateAsync(SyncHistory syncHistory);
    Task<SyncHistory> UpdateAsync(SyncHistory syncHistory);
    Task<List<SyncHistory>> GetByInstitutionAsync(Guid institutionId, int count = 10);
    Task<List<SyncHistory>> GetRecentAsync(int count = 20);
}
