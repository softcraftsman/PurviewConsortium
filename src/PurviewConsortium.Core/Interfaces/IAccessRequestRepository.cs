using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Enums;

namespace PurviewConsortium.Core.Interfaces;

public interface IAccessRequestRepository
{
    Task<AccessRequest?> GetByIdAsync(Guid id);
    Task<List<AccessRequest>> GetByUserAsync(string userId);
    Task<List<AccessRequest>> GetByDataProductAsync(Guid dataProductId);
    Task<List<AccessRequest>> GetByInstitutionAsync(Guid institutionId);
    Task<List<AccessRequest>> GetByStatusAsync(RequestStatus status);
    Task<AccessRequest?> GetActiveRequestAsync(string userId, Guid dataProductId);
    Task<AccessRequest> CreateAsync(AccessRequest request);
    Task<AccessRequest> UpdateAsync(AccessRequest request);
    Task<int> GetPendingCountByUserAsync(string userId);
    Task<int> GetActiveCountByUserAsync(string userId);
    Task<List<AccessRequest>> GetRecentByUserAsync(string userId, int count = 5);
}
