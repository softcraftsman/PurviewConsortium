using Microsoft.EntityFrameworkCore;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Interfaces;
using PurviewConsortium.Infrastructure.Data;

namespace PurviewConsortium.Infrastructure.Repositories;

public class SyncHistoryRepository : ISyncHistoryRepository
{
    private readonly ConsortiumDbContext _db;

    public SyncHistoryRepository(ConsortiumDbContext db) => _db = db;

    public async Task<SyncHistory> CreateAsync(SyncHistory syncHistory)
    {
        syncHistory.Id = syncHistory.Id == Guid.Empty ? Guid.NewGuid() : syncHistory.Id;
        _db.SyncHistories.Add(syncHistory);
        await _db.SaveChangesAsync();
        return syncHistory;
    }

    public async Task<SyncHistory> UpdateAsync(SyncHistory syncHistory)
    {
        _db.SyncHistories.Update(syncHistory);
        await _db.SaveChangesAsync();
        return syncHistory;
    }

    public async Task<List<SyncHistory>> GetByInstitutionAsync(Guid institutionId, int count = 10) =>
        await _db.SyncHistories
            .Include(s => s.Institution)
            .Where(s => s.InstitutionId == institutionId)
            .OrderByDescending(s => s.StartTime)
            .Take(count)
            .ToListAsync();

    public async Task<List<SyncHistory>> GetRecentAsync(int count = 20) =>
        await _db.SyncHistories
            .Include(s => s.Institution)
            .OrderByDescending(s => s.StartTime)
            .Take(count)
            .ToListAsync();
}
