using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurviewConsortium.Api.DTOs;
using PurviewConsortium.Core.Enums;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Api.Controllers;

[ApiController]
[Route("api/admin/logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepo;

    public AuditLogsController(IAuditLogRepository auditLogRepo)
    {
        _auditLogRepo = auditLogRepo;
    }

    /// <summary>
    /// Get recent audit log entries.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecentLogs([FromQuery] int count = 100, [FromQuery] string? action = null)
    {
        var logs = action != null && Enum.TryParse<AuditAction>(action, true, out var parsedAction)
            ? await _auditLogRepo.GetByActionAsync(parsedAction, count)
            : await _auditLogRepo.GetRecentAsync(count);

        var dtos = logs.Select(l => new AuditLogDto(
            l.Id,
            l.Timestamp,
            l.UserId,
            l.UserEmail,
            l.Action.ToString(),
            l.EntityType,
            l.EntityId,
            l.DetailsJson,
            l.IpAddress
        )).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Get audit logs for a specific user.
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserLogs(string userId, [FromQuery] int count = 50)
    {
        var logs = await _auditLogRepo.GetByUserAsync(userId, count);
        var dtos = logs.Select(l => new AuditLogDto(
            l.Id,
            l.Timestamp,
            l.UserId,
            l.UserEmail,
            l.Action.ToString(),
            l.EntityType,
            l.EntityId,
            l.DetailsJson,
            l.IpAddress
        )).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Get available audit action types for filtering.
    /// </summary>
    [HttpGet("actions")]
    public IActionResult GetActionTypes()
    {
        var actions = Enum.GetNames<AuditAction>().ToList();
        return Ok(actions);
    }
}
