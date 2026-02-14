using PurviewConsortium.Core.Entities;

namespace PurviewConsortium.Core.Interfaces;

public interface IInstitutionRepository
{
    Task<Institution?> GetByIdAsync(Guid id);
    Task<Institution?> GetByTenantIdAsync(string tenantId);
    Task<List<Institution>> GetAllAsync(bool activeOnly = true);
    Task<Institution> CreateAsync(Institution institution);
    Task<Institution> UpdateAsync(Institution institution);
    Task DeleteAsync(Guid id);
}
