using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurviewConsortium.Api.DTOs;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Enums;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Api.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public class AccessRequestsController : ControllerBase
{
    private readonly IAccessRequestRepository _requestRepo;
    private readonly IDataProductRepository _dataProductRepo;
    private readonly IInstitutionRepository _institutionRepo;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly ILogger<AccessRequestsController> _logger;

    public AccessRequestsController(
        IAccessRequestRepository requestRepo,
        IDataProductRepository dataProductRepo,
        IInstitutionRepository institutionRepo,
        INotificationService notificationService,
        IAuditLogRepository auditLogRepo,
        ILogger<AccessRequestsController> logger)
    {
        _requestRepo = requestRepo;
        _dataProductRepo = dataProductRepo;
        _institutionRepo = institutionRepo;
        _notificationService = notificationService;
        _auditLogRepo = auditLogRepo;
        _logger = logger;
    }

    /// <summary>Submit a new access request.</summary>
    [HttpPost]
    public async Task<ActionResult<AccessRequestDto>> CreateRequest([FromBody] CreateAccessRequestDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var product = await _dataProductRepo.GetByIdAsync(dto.DataProductId);
        if (product == null || !product.IsListed)
            return NotFound("Data Product not found or not available.");

        // Check for existing active request
        var existing = await _requestRepo.GetActiveRequestAsync(userId, dto.DataProductId);
        if (existing != null)
            return Conflict($"You already have an active request (status: {existing.Status}) for this Data Product.");

        // Determine requesting institution from user's tenant
        var tenantId = User.FindFirst("tid")?.Value;
        Institution? requestingInstitution = null;
        if (tenantId != null)
            requestingInstitution = await _institutionRepo.GetByTenantIdAsync(tenantId);

        var request = new AccessRequest
        {
            DataProductId = dto.DataProductId,
            RequestingUserId = userId,
            RequestingUserEmail = User.FindFirst("preferred_username")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "unknown",
            RequestingUserName = User.FindFirst("name")?.Value ?? "Unknown User",
            RequestingInstitutionId = requestingInstitution?.Id,
            TargetFabricWorkspaceId = dto.TargetFabricWorkspaceId,
            TargetLakehouseName = dto.TargetLakehouseName,
            BusinessJustification = dto.BusinessJustification,
            RequestedDurationDays = dto.RequestedDurationDays,
            Status = RequestStatus.Submitted,
            StatusChangedDate = DateTime.UtcNow
        };

        var created = await _requestRepo.CreateAsync(request);

        // Notify owning institution
        await _notificationService.SendAccessRequestNotificationAsync(
            product.Institution.PrimaryContactEmail,
            product.Name,
            request.RequestingUserName,
            request.BusinessJustification);

        // Audit log
        await _auditLogRepo.LogAsync(new AuditLog
        {
            UserId = userId,
            UserEmail = request.RequestingUserEmail,
            Action = AuditAction.RequestAccess,
            EntityType = nameof(AccessRequest),
            EntityId = created.Id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        // Reload with navigation properties
        var reloaded = await _requestRepo.GetByIdAsync(created.Id);
        return CreatedAtAction(nameof(GetRequest), new { id = created.Id }, MapToDto(reloaded!));
    }

    /// <summary>List access requests (filtered by role).</summary>
    [HttpGet]
    public async Task<ActionResult<List<AccessRequestDto>>> ListRequests(
        [FromQuery] string? status,
        [FromQuery] Guid? dataProductId,
        [FromQuery] Guid? institutionId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        // For now, return user's own requests (role-based filtering to be expanded)
        var requests = await _requestRepo.GetByUserAsync(userId);

        if (status != null && Enum.TryParse<RequestStatus>(status, true, out var statusEnum))
            requests = requests.Where(r => r.Status == statusEnum).ToList();

        if (dataProductId.HasValue)
            requests = requests.Where(r => r.DataProductId == dataProductId.Value).ToList();

        return Ok(requests.Select(MapToDto).ToList());
    }

    /// <summary>Get details of a specific access request.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccessRequestDto>> GetRequest(Guid id)
    {
        var request = await _requestRepo.GetByIdAsync(id);
        if (request == null) return NotFound();

        // Basic authorization: user can see their own requests
        var userId = GetCurrentUserId();
        if (request.RequestingUserId != userId)
        {
            // TODO: Check if user is institution admin or consortium admin
        }

        return Ok(MapToDto(request));
    }

    /// <summary>Get fulfillment details for an approved request.</summary>
    [HttpGet("{id:guid}/fulfillment")]
    public async Task<ActionResult<FulfillmentDetailsDto>> GetFulfillmentDetails(Guid id)
    {
        var request = await _requestRepo.GetByIdAsync(id);
        if (request == null) return NotFound();

        if (request.Status != RequestStatus.Approved && request.Status != RequestStatus.Fulfilled)
            return BadRequest("Fulfillment details are only available for approved requests.");

        var sourceInstitution = request.DataProduct.Institution;
        var requestingInstitution = request.RequestingInstitution;

        var steps = new List<string>
        {
            "1. Open the Fabric portal (https://app.fabric.microsoft.com)",
            $"2. Navigate to workspace: {sourceInstitution.FabricWorkspaceId ?? "(configure workspace ID)"}",
            $"3. Find the data item for '{request.DataProduct.Name}'",
            "4. Click 'Share' → 'External data share'",
            $"5. Enter recipient tenant: {requestingInstitution?.TenantId ?? "(unknown tenant)"}",
            $"6. Enter recipient email: {request.RequestingUserEmail}",
            "7. Set appropriate permissions and confirm the share",
            $"8. The recipient should create a shortcut in their lakehouse: {request.TargetLakehouseName ?? "(not specified)"}",
            $"9. Target workspace: {request.TargetFabricWorkspaceId ?? "(not specified)"}",
            "10. Return to this portal and mark the request as 'Fulfilled' with the share ID"
        };

        return Ok(new FulfillmentDetailsDto(
            RequestId: request.Id,
            DataProductName: request.DataProduct.Name,
            SourceInstitutionName: sourceInstitution.Name,
            SourceFabricWorkspaceId: sourceInstitution.FabricWorkspaceId,
            RecipientTenantId: requestingInstitution?.TenantId ?? "(unknown tenant)",
            RecipientUserEmail: request.RequestingUserEmail,
            TargetFabricWorkspaceId: request.TargetFabricWorkspaceId,
            TargetLakehouseName: request.TargetLakehouseName,
            FulfillmentSteps: steps
        ));
    }

    /// <summary>Update the status of an access request.</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AccessRequestDto>> UpdateStatus(Guid id, [FromBody] UpdateRequestStatusDto dto)
    {
        var request = await _requestRepo.GetByIdAsync(id);
        if (request == null) return NotFound();

        // Validate state transitions
        if (!IsValidTransition(request.Status, dto.NewStatus))
            return BadRequest($"Cannot transition from {request.Status} to {dto.NewStatus}.");

        var userId = GetCurrentUserId() ?? "system";

        request.Status = dto.NewStatus;
        request.StatusChangedDate = DateTime.UtcNow;
        request.StatusChangedBy = userId;

        if (dto.NewStatus == RequestStatus.Fulfilled && !string.IsNullOrEmpty(dto.ExternalShareId))
            request.ExternalShareId = dto.ExternalShareId;

        if (dto.NewStatus == RequestStatus.Fulfilled && request.RequestedDurationDays.HasValue)
            request.ExpirationDate = DateTime.UtcNow.AddDays(request.RequestedDurationDays.Value);

        await _requestRepo.UpdateAsync(request);

        // Notify requesting user
        await _notificationService.SendStatusChangeNotificationAsync(
            request.RequestingUserEmail,
            request.DataProduct.Name,
            dto.NewStatus.ToString(),
            dto.Comment);

        // Audit log
        await _auditLogRepo.LogAsync(new AuditLog
        {
            UserId = userId,
            Action = dto.NewStatus switch
            {
                RequestStatus.Approved => AuditAction.ApproveRequest,
                RequestStatus.Denied => AuditAction.DenyRequest,
                RequestStatus.Fulfilled => AuditAction.FulfillRequest,
                RequestStatus.Revoked => AuditAction.RevokeAccess,
                _ => AuditAction.ApproveRequest
            },
            EntityType = nameof(AccessRequest),
            EntityId = id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        var reloaded = await _requestRepo.GetByIdAsync(id);
        return Ok(MapToDto(reloaded!));
    }

    /// <summary>Cancel a pending access request.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> CancelRequest(Guid id)
    {
        var request = await _requestRepo.GetByIdAsync(id);
        if (request == null) return NotFound();

        var userId = GetCurrentUserId();
        if (request.RequestingUserId != userId)
            return Forbid();

        if (request.Status != RequestStatus.Submitted && request.Status != RequestStatus.UnderReview)
            return BadRequest("Only pending requests can be cancelled.");

        request.Status = RequestStatus.Cancelled;
        request.StatusChangedDate = DateTime.UtcNow;
        request.StatusChangedBy = userId;
        await _requestRepo.UpdateAsync(request);

        await _auditLogRepo.LogAsync(new AuditLog
        {
            UserId = userId,
            Action = AuditAction.CancelRequest,
            EntityType = nameof(AccessRequest),
            EntityId = id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        return NoContent();
    }

    private static bool IsValidTransition(RequestStatus current, RequestStatus next)
    {
        return (current, next) switch
        {
            (RequestStatus.Submitted, RequestStatus.UnderReview) => true,
            (RequestStatus.Submitted, RequestStatus.Approved) => true,
            (RequestStatus.Submitted, RequestStatus.Denied) => true,
            (RequestStatus.Submitted, RequestStatus.Cancelled) => true,
            (RequestStatus.UnderReview, RequestStatus.Approved) => true,
            (RequestStatus.UnderReview, RequestStatus.Denied) => true,
            (RequestStatus.UnderReview, RequestStatus.Cancelled) => true,
            (RequestStatus.Approved, RequestStatus.Fulfilled) => true,
            (RequestStatus.Approved, RequestStatus.Denied) => true,
            (RequestStatus.Fulfilled, RequestStatus.Active) => true,
            (RequestStatus.Fulfilled, RequestStatus.Revoked) => true,
            (RequestStatus.Active, RequestStatus.Revoked) => true,
            (RequestStatus.Active, RequestStatus.Expired) => true,
            _ => false
        };
    }

    private string? GetCurrentUserId() =>
        User.FindFirst("oid")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private static AccessRequestDto MapToDto(AccessRequest r) => new(
        r.Id,
        r.DataProductId,
        r.DataProduct?.Name ?? "Unknown",
        r.DataProduct?.Institution?.Name ?? "Unknown",
        r.RequestingUserId,
        r.RequestingUserEmail,
        r.RequestingUserName,
        r.RequestingInstitutionId,
        r.RequestingInstitution?.Name ?? "Unknown",
        r.TargetFabricWorkspaceId,
        r.TargetLakehouseName,
        r.BusinessJustification,
        r.RequestedDurationDays,
        r.Status,
        r.StatusChangedDate,
        r.StatusChangedBy,
        r.ExternalShareId,
        r.ExpirationDate,
        r.CreatedDate
    );
}
