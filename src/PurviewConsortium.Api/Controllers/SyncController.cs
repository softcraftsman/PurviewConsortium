using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurviewConsortium.Api.DTOs;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Api.Controllers;

[ApiController]
[Route("api/admin/sync")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly ISyncHistoryRepository _syncHistoryRepo;
    private readonly ISyncOrchestrator _syncOrchestrator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncController> _logger;

    public SyncController(
        ISyncHistoryRepository syncHistoryRepo,
        ISyncOrchestrator syncOrchestrator,
        IServiceScopeFactory scopeFactory,
        ILogger<SyncController> logger)
    {
        _syncHistoryRepo = syncHistoryRepo;
        _syncOrchestrator = syncOrchestrator;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Get sync history across all institutions.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<List<SyncHistoryDto>>> GetSyncHistory([FromQuery] Guid? institutionId, [FromQuery] int count = 20)
    {
        var history = institutionId.HasValue
            ? await _syncHistoryRepo.GetByInstitutionAsync(institutionId.Value, count)
            : await _syncHistoryRepo.GetRecentAsync(count);

        return Ok(history.Select(h => new SyncHistoryDto(
            h.Id, h.InstitutionId, h.Institution?.Name ?? "Unknown",
            h.StartTime, h.EndTime, h.Status.ToString(),
            h.ProductsFound, h.ProductsAdded, h.ProductsUpdated,
            h.ProductsDelisted, h.ErrorDetails
        )).ToList());
    }

    /// <summary>Trigger a full scan of all institutions.</summary>
    [HttpPost("trigger")]
    public ActionResult TriggerFullScan()
    {
        // Capture the user's bearer token BEFORE spawning the background task
        // (HttpContext is not available after the request ends).
        // This token will be used in the OBO flow to get a Purview token
        // with the user's permissions (which can see all governance domains).
        var userToken = ExtractBearerToken();

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<ISyncOrchestrator>();
            try
            {
                await orchestrator.ScanAllInstitutionsAsync(userToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Full scan failed");
            }
        });
        return Accepted(new { message = "Full scan triggered for all institutions." });
    }

    private string? ExtractBearerToken()
    {
        var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }
        return null;
    }
}
