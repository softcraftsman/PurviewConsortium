using Microsoft.EntityFrameworkCore;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Enums;
using PurviewConsortium.Core.Interfaces;
using PurviewConsortium.Infrastructure.Data;

namespace PurviewConsortium.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly ConsortiumDbContext _db;

    public AuditLogRepository(ConsortiumDbContext db) => _db = db;

    public async Task LogAsync(AuditLog entry)
    {
        entry.Id = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id;
        entry.Timestamp = DateTime.UtcNow;
        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetRecentAsync(int count = 50) =>
        await _db.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync();

    public async Task<List<AuditLog>> GetByUserAsync(string userId, int count = 50) =>
        await _db.AuditLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync();

    public async Task<List<AuditLog>> GetByActionAsync(AuditAction action, int count = 50) =>
        await _db.AuditLogs
            .Where(a => a.Action == action)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync();
}
