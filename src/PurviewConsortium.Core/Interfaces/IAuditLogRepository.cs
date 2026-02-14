using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Enums;

namespace PurviewConsortium.Core.Interfaces;

public interface IAuditLogRepository
{
    Task LogAsync(AuditLog entry);
    Task<List<AuditLog>> GetRecentAsync(int count = 50);
    Task<List<AuditLog>> GetByUserAsync(string userId, int count = 50);
    Task<List<AuditLog>> GetByActionAsync(AuditAction action, int count = 50);
}
