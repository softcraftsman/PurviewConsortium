using Microsoft.EntityFrameworkCore;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Interfaces;
using PurviewConsortium.Infrastructure.Data;

namespace PurviewConsortium.Infrastructure.Repositories;

public class DataProductRepository : IDataProductRepository
{
    private readonly ConsortiumDbContext _db;

    public DataProductRepository(ConsortiumDbContext db) => _db = db;

    public async Task<DataProduct?> GetByIdAsync(Guid id) =>
        await _db.DataProducts
            .Include(d => d.Institution)
            .FirstOrDefaultAsync(d => d.Id == id);

    public async Task<DataProduct?> GetByPurviewQualifiedNameAsync(string qualifiedName, Guid institutionId) =>
        await _db.DataProducts
            .FirstOrDefaultAsync(d => d.PurviewQualifiedName == qualifiedName && d.InstitutionId == institutionId);

    public async Task<List<DataProduct>> GetByInstitutionAsync(Guid institutionId, bool listedOnly = true) =>
        await _db.DataProducts
            .Where(d => d.InstitutionId == institutionId && (!listedOnly || d.IsListed))
            .OrderBy(d => d.Name)
            .ToListAsync();

    public async Task<DataProduct> CreateAsync(DataProduct product)
    {
        product.Id = product.Id == Guid.Empty ? Guid.NewGuid() : product.Id;
        product.CreatedDate = DateTime.UtcNow;
        product.ModifiedDate = DateTime.UtcNow;
        _db.DataProducts.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<DataProduct> UpdateAsync(DataProduct product)
    {
        product.ModifiedDate = DateTime.UtcNow;
        _db.DataProducts.Update(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<int> DelistByInstitutionExceptAsync(Guid institutionId, IEnumerable<string> activeQualifiedNames)
    {
        var nameSet = activeQualifiedNames.ToHashSet();
        var toDelists = await _db.DataProducts
            .Where(d => d.InstitutionId == institutionId && d.IsListed && !nameSet.Contains(d.PurviewQualifiedName))
            .ToListAsync();

        foreach (var product in toDelists)
        {
            product.IsListed = false;
            product.ModifiedDate = DateTime.UtcNow;
        }

        return await _db.SaveChangesAsync();
    }

    public async Task<int> GetTotalCountAsync(bool listedOnly = true) =>
        await _db.DataProducts.CountAsync(d => !listedOnly || d.IsListed);

    public async Task<Dictionary<Guid, int>> GetCountByInstitutionAsync() =>
        await _db.DataProducts
            .Where(d => d.IsListed)
            .GroupBy(d => d.InstitutionId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
}
