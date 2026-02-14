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

    public SyncController(ISyncHistoryRepository syncHistoryRepo, ISyncOrchestrator syncOrchestrator)
    {
        _syncHistoryRepo = syncHistoryRepo;
        _syncOrchestrator = syncOrchestrator;
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
        _ = Task.Run(() => _syncOrchestrator.ScanAllInstitutionsAsync());
        return Accepted(new { message = "Full scan triggered for all institutions." });
    }
}
