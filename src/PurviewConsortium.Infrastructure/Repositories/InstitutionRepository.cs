using Microsoft.EntityFrameworkCore;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Interfaces;
using PurviewConsortium.Infrastructure.Data;

namespace PurviewConsortium.Infrastructure.Repositories;

public class InstitutionRepository : IInstitutionRepository
{
    private readonly ConsortiumDbContext _db;

    public InstitutionRepository(ConsortiumDbContext db) => _db = db;

    public async Task<Institution?> GetByIdAsync(Guid id) =>
        await _db.Institutions.FindAsync(id);

    public async Task<Institution?> GetByTenantIdAsync(string tenantId) =>
        await _db.Institutions.FirstOrDefaultAsync(i => i.TenantId == tenantId);

    public async Task<List<Institution>> GetAllAsync(bool activeOnly = true)
    {
        var query = _db.Institutions.AsQueryable();

        if (activeOnly)
            query = query.Where(i => i.IsActive);

        return await query
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<Institution> CreateAsync(Institution institution)
    {
        institution.Id = institution.Id == Guid.Empty ? Guid.NewGuid() : institution.Id;
        institution.CreatedDate = DateTime.UtcNow;
        institution.ModifiedDate = DateTime.UtcNow;
        _db.Institutions.Add(institution);
        await _db.SaveChangesAsync();
        return institution;
    }

    public async Task<Institution> UpdateAsync(Institution institution)
    {
        institution.ModifiedDate = DateTime.UtcNow;
        _db.Institutions.Update(institution);
        await _db.SaveChangesAsync();
        return institution;
    }

    public async Task DeleteAsync(Guid id)
    {
        var institution = await _db.Institutions.FindAsync(id);
        if (institution != null)
        {
            institution.IsActive = false;
            institution.ModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
