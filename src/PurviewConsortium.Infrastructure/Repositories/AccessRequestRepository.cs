using Microsoft.EntityFrameworkCore;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Enums;
using PurviewConsortium.Core.Interfaces;
using PurviewConsortium.Infrastructure.Data;

namespace PurviewConsortium.Infrastructure.Repositories;

public class AccessRequestRepository : IAccessRequestRepository
{
    private readonly ConsortiumDbContext _db;

    public AccessRequestRepository(ConsortiumDbContext db) => _db = db;

    public async Task<AccessRequest?> GetByIdAsync(Guid id) =>
        await _db.AccessRequests
            .Include(r => r.DataProduct).ThenInclude(d => d.Institution)
            .Include(r => r.RequestingInstitution)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<List<AccessRequest>> GetByUserAsync(string userId) =>
        await _db.AccessRequests
            .Include(r => r.DataProduct).ThenInclude(d => d.Institution)
            .Where(r => r.RequestingUserId == userId)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();

    public async Task<List<AccessRequest>> GetByDataProductAsync(Guid dataProductId) =>
        await _db.AccessRequests
            .Include(r => r.RequestingInstitution)
            .Where(r => r.DataProductId == dataProductId)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();

    public async Task<List<AccessRequest>> GetByInstitutionAsync(Guid institutionId) =>
        await _db.AccessRequests
            .Include(r => r.DataProduct)
            .Include(r => r.RequestingInstitution)
            .Where(r => r.DataProduct.InstitutionId == institutionId)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();

    public async Task<List<AccessRequest>> GetByStatusAsync(RequestStatus status) =>
        await _db.AccessRequests
            .Include(r => r.DataProduct).ThenInclude(d => d.Institution)
            .Include(r => r.RequestingInstitution)
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedDate)
            .ToListAsync();

    public async Task<AccessRequest?> GetActiveRequestAsync(string userId, Guid dataProductId)
    {
        var activeStatuses = new[]
        {
            RequestStatus.Submitted,
            RequestStatus.UnderReview,
            RequestStatus.Approved,
            RequestStatus.Fulfilled,
            RequestStatus.Active
        };

        return await _db.AccessRequests
            .FirstOrDefaultAsync(r =>
                r.RequestingUserId == userId &&
                r.DataProductId == dataProductId &&
                activeStatuses.Contains(r.Status));
    }

    public async Task<AccessRequest> CreateAsync(AccessRequest request)
    {
        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        request.CreatedDate = DateTime.UtcNow;
        request.ModifiedDate = DateTime.UtcNow;
        _db.AccessRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<AccessRequest> UpdateAsync(AccessRequest request)
    {
        request.ModifiedDate = DateTime.UtcNow;
        _db.AccessRequests.Update(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<int> GetPendingCountByUserAsync(string userId) =>
        await _db.AccessRequests.CountAsync(r =>
            r.RequestingUserId == userId &&
            (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.UnderReview));

    public async Task<int> GetActiveCountByUserAsync(string userId) =>
        await _db.AccessRequests.CountAsync(r =>
            r.RequestingUserId == userId &&
            (r.Status == RequestStatus.Fulfilled || r.Status == RequestStatus.Active));

    public async Task<List<AccessRequest>> GetRecentByUserAsync(string userId, int count = 5) =>
        await _db.AccessRequests
            .Include(r => r.DataProduct).ThenInclude(d => d.Institution)
            .Where(r => r.RequestingUserId == userId)
            .OrderByDescending(r => r.CreatedDate)
            .Take(count)
            .ToListAsync();
}
