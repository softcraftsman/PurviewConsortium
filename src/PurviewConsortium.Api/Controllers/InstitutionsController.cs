using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurviewConsortium.Api.DTOs;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Enums;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Api.Controllers;

[ApiController]
[Route("api/admin/institutions")]
[Authorize] // TODO: Add [Authorize(Policy = "RequireConsortiumAdmin")] when roles are wired up
public class InstitutionsController : ControllerBase
{
    private readonly IInstitutionRepository _institutionRepo;
    private readonly ISyncOrchestrator _syncOrchestrator;
    private readonly ISyncHistoryRepository _syncHistoryRepo;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InstitutionsController> _logger;

    public InstitutionsController(
        IInstitutionRepository institutionRepo,
        ISyncOrchestrator syncOrchestrator,
        ISyncHistoryRepository syncHistoryRepo,
        IAuditLogRepository auditLogRepo,
        IServiceScopeFactory scopeFactory,
        ILogger<InstitutionsController> logger)
    {
        _institutionRepo = institutionRepo;
        _syncOrchestrator = syncOrchestrator;
        _syncHistoryRepo = syncHistoryRepo;
        _auditLogRepo = auditLogRepo;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>List all institutions.</summary>
    [HttpGet]
    public async Task<ActionResult<List<InstitutionDto>>> ListInstitutions([FromQuery] bool activeOnly = false)
    {
        var institutions = await _institutionRepo.GetAllAsync(activeOnly);
        return Ok(institutions.Select(MapToDto).ToList());
    }

    /// <summary>Get a specific institution.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InstitutionDto>> GetInstitution(Guid id)
    {
        var institution = await _institutionRepo.GetByIdAsync(id);
        if (institution == null) return NotFound();
        return Ok(MapToDto(institution));
    }

    /// <summary>Register a new institution.</summary>
    [HttpPost]
    public async Task<ActionResult<InstitutionDto>> CreateInstitution([FromBody] CreateInstitutionDto dto)
    {
        var institution = new Institution
        {
            Name = dto.Name,
            TenantId = dto.TenantId,
            PurviewAccountName = dto.PurviewAccountName,
            FabricWorkspaceId = dto.FabricWorkspaceId,
            ConsortiumDomainIds = dto.ConsortiumDomainIds,
            PrimaryContactEmail = dto.PrimaryContactEmail,
            IsActive = true,
            AdminConsentGranted = false
        };

        var created = await _institutionRepo.CreateAsync(institution);

        await _auditLogRepo.LogAsync(new AuditLog
        {
            UserId = GetCurrentUserId(),
            Action = AuditAction.RegisterInstitution,
            EntityType = nameof(Institution),
            EntityId = created.Id.ToString(),
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                Name = created.Name,
                PurviewAccount = created.PurviewAccountName,
                Contact = created.PrimaryContactEmail
            }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        return CreatedAtAction(nameof(GetInstitution), new { id = created.Id }, MapToDto(created));
    }

    /// <summary>Update an institution's configuration.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InstitutionDto>> UpdateInstitution(Guid id, [FromBody] UpdateInstitutionDto dto)
    {
        var institution = await _institutionRepo.GetByIdAsync(id);
        if (institution == null) return NotFound();

        institution.Name = dto.Name;
        institution.PurviewAccountName = dto.PurviewAccountName;
        institution.FabricWorkspaceId = dto.FabricWorkspaceId;
        institution.ConsortiumDomainIds = dto.ConsortiumDomainIds;
        institution.PrimaryContactEmail = dto.PrimaryContactEmail;
        institution.IsActive = dto.IsActive;
        institution.AdminConsentGranted = dto.AdminConsentGranted;

        var updated = await _institutionRepo.UpdateAsync(institution);

        await _auditLogRepo.LogAsync(new AuditLog
        {
            UserId = GetCurrentUserId(),
            Action = AuditAction.UpdateInstitution,
            EntityType = nameof(Institution),
            EntityId = id.ToString(),
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                Name = updated.Name,
                IsActive = updated.IsActive,
                PurviewAccount = updated.PurviewAccountName
            }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        return Ok(MapToDto(updated));
    }

    /// <summary>Deactivate an institution.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeactivateInstitution(Guid id)
    {
        await _institutionRepo.DeleteAsync(id);

        await _auditLogRepo.LogAsync(new AuditLog
        {
            UserId = GetCurrentUserId(),
            Action = AuditAction.DeactivateInstitution,
            EntityType = nameof(Institution),
            EntityId = id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        return NoContent();
    }

    /// <summary>Trigger an on-demand Purview scan for an institution.</summary>
    [HttpPost("{id:guid}/scan")]
    public async Task<ActionResult> TriggerScan(Guid id)
    {
        var institution = await _institutionRepo.GetByIdAsync(id);
        if (institution == null) return NotFound();

        // Capture user's bearer token before spawning background task (for OBO flow)
        var userToken = ExtractBearerToken();

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<ISyncOrchestrator>();
            try
            {
                await orchestrator.ScanInstitutionAsync(id, userToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "On-demand scan failed for institution {Id}", id);
            }
        });

        await _auditLogRepo.LogAsync(new AuditLog
        {
            UserId = GetCurrentUserId(),
            Action = AuditAction.TriggerScan,
            EntityType = nameof(Institution),
            EntityId = id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        return Accepted(new { message = $"Scan triggered for {institution.Name}. Check sync history for results." });
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

    private string? GetCurrentUserId() =>
        User.FindFirst("oid")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private static InstitutionDto MapToDto(Institution i) => new(
        i.Id, i.Name, i.TenantId, i.PurviewAccountName,
        i.FabricWorkspaceId, i.ConsortiumDomainIds, i.PrimaryContactEmail,
        i.IsActive, i.AdminConsentGranted,
        i.CreatedDate, i.ModifiedDate);
}
