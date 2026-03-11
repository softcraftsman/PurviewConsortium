using PurviewConsortium.Core.Entities;

namespace PurviewConsortium.Core.Interfaces;

public interface IDataAssetRepository
{
    Task<DataAsset?> GetByIdAsync(Guid id);
    Task<DataAsset?> GetByPurviewAssetIdAsync(string purviewAssetId, Guid institutionId);
    Task<List<DataAsset>> GetByInstitutionAsync(Guid institutionId);
    Task<List<DataAsset>> GetAllAsync();
    Task<DataAsset> CreateAsync(DataAsset asset);
    Task<DataAsset> UpdateAsync(DataAsset asset);
    Task<int> DeleteByInstitutionExceptAsync(Guid institutionId, IEnumerable<string> activePurviewAssetIds);
    Task<int> GetTotalCountAsync();
}
