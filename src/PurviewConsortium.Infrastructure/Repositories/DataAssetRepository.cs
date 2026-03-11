using Microsoft.EntityFrameworkCore;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Interfaces;
using PurviewConsortium.Infrastructure.Data;

namespace PurviewConsortium.Infrastructure.Repositories;

public class DataAssetRepository : IDataAssetRepository
{
    private readonly ConsortiumDbContext _db;

    public DataAssetRepository(ConsortiumDbContext db) => _db = db;

    public async Task<DataAsset?> GetByIdAsync(Guid id) =>
        await _db.DataAssets
            .Include(a => a.Institution)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<DataAsset?> GetByPurviewAssetIdAsync(string purviewAssetId, Guid institutionId) =>
        await _db.DataAssets
            .FirstOrDefaultAsync(a => a.PurviewAssetId == purviewAssetId && a.InstitutionId == institutionId);

    public async Task<List<DataAsset>> GetByInstitutionAsync(Guid institutionId) =>
        await _db.DataAssets
            .Where(a => a.InstitutionId == institutionId)
            .OrderBy(a => a.Name)
            .ToListAsync();

    public async Task<List<DataAsset>> GetAllAsync() =>
        await _db.DataAssets
            .Include(a => a.Institution)
            .OrderBy(a => a.Name)
            .ToListAsync();

    public async Task<DataAsset> CreateAsync(DataAsset asset)
    {
        asset.Id = asset.Id == Guid.Empty ? Guid.NewGuid() : asset.Id;
        asset.CreatedDate = DateTime.UtcNow;
        asset.ModifiedDate = DateTime.UtcNow;
        _db.DataAssets.Add(asset);
        await _db.SaveChangesAsync();
        return asset;
    }

    public async Task<DataAsset> UpdateAsync(DataAsset asset)
    {
        asset.ModifiedDate = DateTime.UtcNow;
        _db.DataAssets.Update(asset);
        await _db.SaveChangesAsync();
        return asset;
    }

    public async Task<int> DeleteByInstitutionExceptAsync(Guid institutionId, IEnumerable<string> activePurviewAssetIds)
    {
        var activeSet = activePurviewAssetIds.ToHashSet();
        var toDelete = await _db.DataAssets
            .Where(a => a.InstitutionId == institutionId && !activeSet.Contains(a.PurviewAssetId))
            .ToListAsync();

        _db.DataAssets.RemoveRange(toDelete);
        return await _db.SaveChangesAsync();
    }

    public async Task<int> GetTotalCountAsync() =>
        await _db.DataAssets.CountAsync();
}
