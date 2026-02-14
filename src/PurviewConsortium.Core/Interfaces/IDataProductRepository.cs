using PurviewConsortium.Core.Entities;

namespace PurviewConsortium.Core.Interfaces;

public interface IDataProductRepository
{
    Task<DataProduct?> GetByIdAsync(Guid id);
    Task<DataProduct?> GetByPurviewQualifiedNameAsync(string qualifiedName, Guid institutionId);
    Task<List<DataProduct>> GetByInstitutionAsync(Guid institutionId, bool listedOnly = true);
    Task<DataProduct> CreateAsync(DataProduct product);
    Task<DataProduct> UpdateAsync(DataProduct product);
    Task<int> DelistByInstitutionExceptAsync(Guid institutionId, IEnumerable<string> activeQualifiedNames);
    Task<int> GetTotalCountAsync(bool listedOnly = true);
    Task<Dictionary<Guid, int>> GetCountByInstitutionAsync();
}
