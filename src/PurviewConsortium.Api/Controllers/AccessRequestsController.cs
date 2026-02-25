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
    private readonly IPurviewWorkflowService _workflowService;
    private readonly IFabricShortcutService _fabricShortcutService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccessRequestsController> _logger;

    public AccessRequestsController(
        IAccessRequestRepository requestRepo,
        IDataProductRepository dataProductRepo,
        IInstitutionRepository institutionRepo,
        INotificationService notificationService,
        IAuditLogRepository auditLogRepo,
        IPurviewWorkflowService workflowService,
        IFabricShortcutService fabricShortcutService,
        IConfiguration configuration,
        ILogger<AccessRequestsController> logger)
    {
        _requestRepo = requestRepo;
        _dataProductRepo = dataProductRepo;
        _institutionRepo = institutionRepo;
        _notificationService = notificationService;
        _auditLogRepo = auditLogRepo;
        _workflowService = workflowService;
        _fabricShortcutService = fabricShortcutService;
        _configuration = configuration;
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

        // Determine requesting institution from user's tenant
        var tenantId = User.FindFirst("tid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
        _logger.LogInformation(
            "CreateRequest: tid claim = {TenantId}, all claims: {Claims}",
            tenantId ?? "(null)",
            string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));

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
            RequestingTenantId = tenantId,
            TargetFabricWorkspaceId = dto.TargetFabricWorkspaceId,
            TargetLakehouseItemId = dto.TargetLakehouseItemId,
            BusinessJustification = dto.BusinessJustification,
            RequestedDurationDays = dto.RequestedDurationDays,
            Status = RequestStatus.Submitted,
            StatusChangedDate = DateTime.UtcNow
        };

        var created = await _requestRepo.CreateAsync(request);

        // Submit to Purview Workflow API for the owning institution's approval process
        try
        {
            var institution = product.Institution;
            if (!string.IsNullOrEmpty(institution.PurviewAccountName))
            {
                // Extract the user's bearer token for OBO flow
                var userToken = HttpContext.Request.Headers["Authorization"]
                    .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

                var workflowResult = await _workflowService.SubmitAccessRequestAsync(
                    institution.PurviewAccountName,
                    institution.TenantId,
                    product.Name,
                    dto.BusinessJustification,
                    userToken);

                if (workflowResult.Success)
                {
                    created.PurviewWorkflowRunId = workflowResult.WorkflowRunId;
                    await _requestRepo.UpdateAsync(created);
                    _logger.LogInformation(
                        "Purview workflow triggered for access request {RequestId}. WorkflowRunId: {WorkflowRunId}",
                        created.Id, workflowResult.WorkflowRunId);
                }
                else
                {
                    _logger.LogWarning(
                        "Purview workflow submission failed for request {RequestId}: {Error}. " +
                        "The local request was still created successfully.",
                        created.Id, workflowResult.ErrorMessage);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Institution '{Institution}' has no Purview account configured. Skipping workflow submission.",
                    institution.Name);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the request creation if workflow submission fails
            _logger.LogError(ex,
                "Unexpected error submitting Purview workflow for request {RequestId}. " +
                "The local request was still created successfully.", created.Id);
        }

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
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                DataProduct = product.Name,
                Institution = product.Institution.Name,
                Justification = request.BusinessJustification,
                TargetWorkspace = request.TargetFabricWorkspaceId,
                TargetLakehouseItemId = request.TargetLakehouseItemId
            }),
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

        // Sync Purview workflow statuses for requests that have a workflow run and aren't terminal
        var userToken = HttpContext.Request.Headers["Authorization"]
            .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        await SyncPurviewWorkflowStatusesAsync(requests, userToken);

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
            $"8. The recipient should create a shortcut in their lakehouse: {request.TargetLakehouseItemId ?? "(not specified)"}",
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
            TargetLakehouseItemId: request.TargetLakehouseItemId,
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
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                DataProduct = request.DataProduct.Name,
                NewStatus = dto.NewStatus.ToString(),
                Comment = dto.Comment,
                RequestedBy = request.RequestingUserEmail
            }),
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
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                DataProduct = request.DataProduct?.Name,
                PreviousStatus = "Cancelled"
            }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        return NoContent();
    }

    /// <summary>Retry automated Fabric shortcut creation for an approved request.</summary>
    [HttpPost("{id:guid}/retry-fulfillment")]
    public async Task<ActionResult> RetryFulfillment(Guid id)
    {
        var request = await _requestRepo.GetByIdAsync(id);
        if (request == null) return NotFound();

        if (request.Status != RequestStatus.Approved)
            return BadRequest("Only approved requests can be retried for fulfillment.");

        if (request.FabricShortcutCreated)
            return BadRequest("Fabric shortcut has already been created.");

        // If the request doesn't have a stored tenant ID, try to get it from the current user's token
        if (string.IsNullOrEmpty(request.RequestingTenantId))
        {
            var tenantId = User.FindFirst("tid")?.Value
                ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
            if (!string.IsNullOrEmpty(tenantId))
            {
                request.RequestingTenantId = tenantId;
                _logger.LogInformation(
                    "RetryFulfillment: Backfilled RequestingTenantId={TenantId} for request {RequestId}",
                    tenantId, id);
            }
        }

        var (success, errorMessage) = await TryAutoFulfillAsync(request);
        if (!success)
            return BadRequest(errorMessage ?? "Auto-fulfillment failed. Check logs for details.");

        var reloaded = await _requestRepo.GetByIdAsync(id);
        return Ok(MapToDto(reloaded!));
    }

    /// <summary>
    /// Checks Purview for workflow run status updates and syncs them to local records.
    /// Only checks requests that have a workflow run ID and aren't already terminal.
    /// </summary>
    private async Task SyncPurviewWorkflowStatusesAsync(IList<AccessRequest> requests, string? userToken)
    {
        var terminalWorkflowStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Completed", "Canceled", "Failed" };

        var toSync = requests
            .Where(r => !string.IsNullOrEmpty(r.PurviewWorkflowRunId) &&
                        (r.PurviewWorkflowStatus == null || !terminalWorkflowStatuses.Contains(r.PurviewWorkflowStatus)))
            .ToList();

        if (toSync.Count == 0) return;

        foreach (var req in toSync)
        {
            try
            {
                var institution = req.DataProduct?.Institution;
                if (institution == null || string.IsNullOrEmpty(institution.PurviewAccountName))
                    continue;

                var result = await _workflowService.GetWorkflowRunStatusAsync(
                    institution.PurviewAccountName,
                    institution.TenantId,
                    req.PurviewWorkflowRunId!,
                    userToken);

                if (!result.Success) continue;

                var changed = false;

                // Update workflow status if changed
                if (req.PurviewWorkflowStatus != result.RunStatus)
                {
                    req.PurviewWorkflowStatus = result.RunStatus;
                    changed = true;
                }

                // Map completed workflow to local request status
                if (string.Equals(result.RunStatus, "Completed", StringComparison.OrdinalIgnoreCase) &&
                    req.Status == RequestStatus.Submitted)
                {
                    // Normalize the approval outcome for comparison
                    var isRejected = string.Equals(result.ApprovalOutcome, "Rejected", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(result.ApprovalOutcome, "Denied", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(result.ApprovalOutcome, "Reject", StringComparison.OrdinalIgnoreCase);

                    if (isRejected)
                    {
                        req.Status = RequestStatus.Denied;
                        req.StatusChangedDate = DateTime.UtcNow;
                        req.StatusChangedBy = "PurviewWorkflow";
                        changed = true;
                    }
                    else
                    {
                        // Treat Completed workflow as Approved when outcome is "Approved"
                        // or when outcome is null/unrecognized — a Purview workflow that
                        // reaches Completed status without an explicit rejection means
                        // the approval was granted.
                        if (result.ApprovalOutcome == null)
                        {
                            _logger.LogWarning(
                                "Workflow run {RunId} for request {RequestId} completed but " +
                                "approval outcome is null. Treating as Approved. " +
                                "This may indicate the Purview API response structure has changed.",
                                req.PurviewWorkflowRunId, req.Id);
                        }

                        req.Status = RequestStatus.Approved;
                        req.StatusChangedDate = DateTime.UtcNow;
                        req.StatusChangedBy = "PurviewWorkflow";
                        changed = true;

                        // Attempt automated cross-tenant shortcut creation
                        var autoFulfill = _configuration.GetValue<bool>("Fabric:AutoFulfillOnApproval", false);
                        if (autoFulfill)
                        {
                            var (fulfilled, fulfillError) = await TryAutoFulfillAsync(req);
                            if (fulfilled)
                            {
                                _logger.LogInformation(
                                    "Auto-fulfillment completed for request {RequestId}", req.Id);
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "Auto-fulfillment not completed for request {RequestId}: {Error}",
                                    req.Id, fulfillError);
                            }
                        }
                    }
                }
                else if (string.Equals(result.RunStatus, "Canceled", StringComparison.OrdinalIgnoreCase) &&
                         req.Status == RequestStatus.Submitted)
                {
                    req.Status = RequestStatus.Cancelled;
                    req.StatusChangedDate = DateTime.UtcNow;
                    req.StatusChangedBy = "PurviewWorkflow";
                    changed = true;
                }

                if (changed)
                {
                    await _requestRepo.UpdateAsync(req);
                    _logger.LogInformation(
                        "Synced Purview workflow status for request {RequestId}: " +
                        "workflowStatus={WorkflowStatus}, approvalOutcome={Outcome}, localStatus={Status}",
                        req.Id, result.RunStatus, result.ApprovalOutcome ?? "(none)", req.Status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to sync Purview workflow status for request {RequestId} (run {RunId}). Skipping.",
                    req.Id, req.PurviewWorkflowRunId);
            }
        }
    }

    /// <summary>
    /// Attempts automated fulfillment by creating a cross-tenant Fabric shortcut.
    /// Returns (success, errorMessage).
    /// </summary>
    private async Task<(bool Success, string? Error)> TryAutoFulfillAsync(AccessRequest req)
    {
        try
        {
            var institution = req.DataProduct?.Institution;
            var requestingInstitution = req.RequestingInstitution;

            // Validate all required fields are present for automated fulfillment
            if (institution == null || string.IsNullOrEmpty(institution.FabricWorkspaceId))
            {
                var msg = $"Source institution or FabricWorkspaceId is missing";
                _logger.LogWarning("Cannot auto-fulfill request {RequestId}: {Reason}", req.Id, msg);
                return (false, msg);
            }

            if (string.IsNullOrEmpty(req.TargetFabricWorkspaceId) ||
                string.IsNullOrEmpty(req.TargetLakehouseItemId))
            {
                var msg = "Target workspace or target lakehouse item ID not specified on the request";
                _logger.LogWarning("Cannot auto-fulfill request {RequestId}: {Reason}", req.Id, msg);
                return (false, msg);
            }

            // Resolve recipient tenant ID: prefer stored tenant ID on request, fall back to institution
            var recipientTenantId = req.RequestingTenantId
                ?? requestingInstitution?.TenantId;

            if (string.IsNullOrEmpty(recipientTenantId))
            {
                var msg = $"Requesting tenant ID unknown (RequestingTenantId={req.RequestingTenantId ?? "(null)"}, " +
                          $"InstitutionTenantId={requestingInstitution?.TenantId ?? "(null)"})";
                _logger.LogWarning("Cannot auto-fulfill request {RequestId}: {Reason}", req.Id, msg);
                return (false, msg);
            }

            // Source item ID: the Data Product's source lakehouse in Fabric
            // (the Purview Data Asset IS the source lakehouse)
            var sourceItemId = req.DataProduct?.SourceLakehouseItemId
                ?? _configuration["Fabric:SourceItemOverride"];

            if (string.IsNullOrEmpty(sourceItemId))
            {
                var msg = "No source lakehouse item ID configured on the Data Product. " +
                          "Set DataProduct.SourceLakehouseItemId or Fabric:SourceItemOverride app setting.";
                _logger.LogWarning("Cannot auto-fulfill request {RequestId}: {Reason}", req.Id, msg);
                return (false, msg);
            }

            _logger.LogInformation(
                "Auto-fulfillment for request {RequestId}: sourceWorkspace={Workspace}, " +
                "sourceLakehouse={ItemId}, recipientTenant={Tenant}, targetWorkspace={TargetWs}, " +
                "targetLakehouse={TargetLh}",
                req.Id, institution.FabricWorkspaceId, sourceItemId, recipientTenantId,
                req.TargetFabricWorkspaceId, req.TargetLakehouseItemId);

            var result = await _fabricShortcutService.CreateCrossTenantShortcutAsync(
                sourceWorkspaceId: institution.FabricWorkspaceId,
                sourceItemId: sourceItemId,
                sourceTenantId: institution.TenantId,
                recipientTenantId: recipientTenantId,
                recipientUserEmail: req.RequestingUserEmail,
                targetWorkspaceId: req.TargetFabricWorkspaceId,
                targetLakehouseId: req.TargetLakehouseItemId,  // Consumer's target lakehouse item ID
                dataProductName: req.DataProduct?.Name ?? "DataProduct",
                cancellationToken: default);

            if (result.Success || result.PartialSuccess)
            {
                req.ExternalShareId = result.ExternalShareId;
                req.FabricShortcutName = result.ShortcutName;
                req.FabricShortcutCreated = result.Success;

                if (result.Success)
                {
                    req.Status = RequestStatus.Fulfilled;
                    req.StatusChangedDate = DateTime.UtcNow;
                    req.StatusChangedBy = "AutoFulfillment";

                    if (req.RequestedDurationDays.HasValue)
                        req.ExpirationDate = DateTime.UtcNow.AddDays(req.RequestedDurationDays.Value);
                }

                await _requestRepo.UpdateAsync(req);

                // Notify requesting user of fulfillment
                await _notificationService.SendStatusChangeNotificationAsync(
                    req.RequestingUserEmail,
                    req.DataProduct?.Name ?? "Data Product",
                    result.Success ? "Fulfilled" : "Approved (share created, shortcut pending)",
                    result.Success
                        ? $"Your cross-tenant shortcut '{result.ShortcutName}' has been created automatically."
                        : "The external data share was created. You may need to create the shortcut manually.");

                // Audit
                await _auditLogRepo.LogAsync(new AuditLog
                {
                    UserId = "AutoFulfillment",
                    UserEmail = "system",
                    Action = AuditAction.FulfillRequest,
                    EntityType = nameof(AccessRequest),
                    EntityId = req.Id.ToString(),
                    DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        AutoFulfilled = true,
                        result.ExternalShareId,
                        result.ShortcutName,
                        result.PartialSuccess
                    })
                });

                return (result.Success, null);
            }

            var failMsg = $"Fabric API call failed: {result.ErrorMessage}";
            _logger.LogWarning(
                "Auto-fulfillment failed for request {RequestId}: {Error}",
                req.Id, result.ErrorMessage);
            return (false, failMsg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during auto-fulfillment for request {RequestId}. " +
                "Request remains in Approved state for manual fulfillment.", req.Id);
            return (false, $"Unexpected error: {ex.Message}");
        }
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
        r.TargetLakehouseItemId,
        r.BusinessJustification,
        r.RequestedDurationDays,
        r.Status,
        r.StatusChangedDate,
        r.StatusChangedBy,
        r.ExternalShareId,
        r.FabricShortcutName,
        r.FabricShortcutCreated,
        r.PurviewWorkflowRunId,
        r.PurviewWorkflowStatus,
        r.ExpirationDate,
        r.CreatedDate
    );
}
