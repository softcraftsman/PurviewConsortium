using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurviewConsortium.Api.DTOs;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Enums;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Api.Controllers;

[ApiController]
[Route("api/requests")]
public class AccessRequestsController : ControllerBase
{
    private readonly IAccessRequestRepository _requestRepo;
    private readonly IDataProductRepository _dataProductRepo;
    private readonly IInstitutionRepository _institutionRepo;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IPurviewDataAccessService _dataAccessService;
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
        IPurviewDataAccessService dataAccessService,
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
        _dataAccessService = dataAccessService;
        _workflowService = workflowService;
        _fabricShortcutService = fabricShortcutService;
        _configuration = configuration;
        _logger = logger;
    }

    private string? ResolveSourceWorkspaceId(DataProduct product)
    {
        var sourceAssetWorkspaceId = product.DataProductDataAssets
            .Select(link => link.DataAsset?.SourceWorkspaceId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

        return sourceAssetWorkspaceId ?? _configuration["Fabric:SourceWorkspaceOverride"];
    }

    /// <summary>Submit a new access request.</summary>
    [HttpPost]
    public async Task<ActionResult<CreateAccessRequestResponseDto>> CreateRequest([FromBody] CreateAccessRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var entraObjectId = GetCurrentEntraObjectId();
        string? purviewSubmissionWarning = null;
        if (userId == null)
        {
            _logger.LogWarning(
                "CreateRequest unauthorized: could not resolve current user ID from claims. Claims: {Claims}",
                string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
            return Unauthorized();
        }

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
            RequestingUserId = entraObjectId ?? userId,
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
            StatusChangedDate = DateTime.UtcNow,
            // Determine share type: internal if requesting tenant matches the owning institution's tenant
            ShareType = !string.IsNullOrEmpty(tenantId)
                && string.Equals(tenantId, product.Institution.TenantId, StringComparison.OrdinalIgnoreCase)
                ? Core.Enums.ShareType.Internal
                : Core.Enums.ShareType.External
        };

        var created = await _requestRepo.CreateAsync(request);

        // Create Purview Data Access subscription for the owning institution
        try
        {
            var institution = product.Institution;
            if (!string.IsNullOrEmpty(institution.PurviewAccountName))
            {
                // Extract the user's bearer token for OBO flow
                var userToken = HttpContext.Request.Headers["Authorization"]
                    .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

                // The Data Access API requires the Purview Data Product ID (GUID string).
                var purviewDataProductId = product.PurviewQualifiedName;
                var subscriberObjectId = entraObjectId;
                var purpose = "Research";
                var purviewBusinessJustification = BuildPurviewBusinessJustification(created, product);

                if (string.IsNullOrWhiteSpace(purviewDataProductId) ||
                    !Guid.TryParse(purviewDataProductId, out _))
                {
                    purviewSubmissionWarning =
                        $"Purview subscription was not created because the data product Purview ID is invalid: '{purviewDataProductId}'.";
                    _logger.LogWarning(
                        "Skipping Purview data subscription creation. RequestId={RequestId}, DataProduct={DataProduct}, " +
                        "Reason=InvalidPurviewDataProductId, PurviewQualifiedName={PurviewQualifiedName}",
                        created.Id,
                        product.Name,
                        purviewDataProductId);
                }
                else if (string.IsNullOrWhiteSpace(subscriberObjectId) ||
                    !Guid.TryParse(subscriberObjectId, out _))
                {
                    purviewSubmissionWarning =
                        $"Purview subscription was not created because your Entra object ID is missing or invalid: '{subscriberObjectId ?? "(null)"}'.";
                    _logger.LogWarning(
                        "Skipping Purview data subscription creation. RequestId={RequestId}, DataProduct={DataProduct}, " +
                        "Reason=InvalidSubscriberObjectId, ResolvedSubscriberObjectId={SubscriberObjectId}, StoredRequestingUserId={RequestingUserId}",
                        created.Id,
                        product.Name,
                        subscriberObjectId ?? "(null)",
                        request.RequestingUserId);
                }
                else
                {
                    var subscriptionResult = await _dataAccessService.CreateDataSubscriptionAsync(
                        tenantId: institution.TenantId,
                        subscriptionId: Guid.NewGuid().ToString(),
                        dataProductId: purviewDataProductId,
                        subscriberObjectId: subscriberObjectId,
                        identityType: "User",
                        businessJustification: purviewBusinessJustification,
                        purpose: purpose,
                        userAccessToken: userToken);

                    if (subscriptionResult.Success)
                    {
                        created.ExternalShareId = subscriptionResult.SubscriptionId;
                        await _requestRepo.UpdateAsync(created);
                        _logger.LogInformation(
                            "Purview data subscription created. RequestId={RequestId}, SubscriptionId={SubscriptionId}, DataProduct={DataProduct}, Institution={Institution}, InstitutionTenantId={InstitutionTenantId}, RequestingTenantId={RequestingTenantId}",
                            created.Id,
                            subscriptionResult.SubscriptionId,
                            product.Name,
                            institution.Name,
                            institution.TenantId,
                            tenantId ?? "");
                    }
                    else
                    {
                        purviewSubmissionWarning = subscriptionResult.ErrorMessage;
                        _logger.LogWarning(
                            "Purview data subscription creation failed. RequestId={RequestId}, DataProduct={DataProduct}, Institution={Institution}, InstitutionTenantId={InstitutionTenantId}, RequestingTenantId={RequestingTenantId}, Error={Error}. " +
                            "The local request was still created successfully.",
                            created.Id,
                            product.Name,
                            institution.Name,
                            institution.TenantId,
                            tenantId ?? "",
                            subscriptionResult.ErrorMessage);
                    }
                }
            }
            else
            {
                purviewSubmissionWarning =
                    "Purview subscription was not created because the owning institution does not have a Purview account configured.";
                _logger.LogInformation(
                    "Skipping Purview data subscription creation. RequestId={RequestId}, Institution={Institution}, Reason=PurviewAccountMissing",
                    created.Id,
                    institution.Name);
            }
        }
        catch (Exception ex)
        {
            purviewSubmissionWarning = $"Purview subscription was not created due to an unexpected error: {ex.Message}";
            // Don't fail the request creation if workflow submission fails
            _logger.LogError(ex,
                "Unexpected error creating Purview data subscription for request {RequestId}. " +
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
                RequestId = created.Id,
                PurviewSubscriptionId = created.ExternalShareId,
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
        var response = new CreateAccessRequestResponseDto(
            MapToDto(reloaded!),
            purviewSubmissionWarning);

        return CreatedAtAction(nameof(GetRequest), new { id = created.Id }, response);
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
        List<AccessRequest> requests;
        try
        {
            requests = await _requestRepo.GetByUserAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to load access requests for user {UserId}. This usually indicates malformed enum/string data in AccessRequests.",
                userId);
            return Problem("Failed to load access requests. Please contact support with the request timestamp.");
        }

        // Sync Purview workflow statuses for requests that have a workflow run and aren't terminal
        var userToken = HttpContext.Request.Headers["Authorization"]
            .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        await SyncPurviewWorkflowStatusesAsync(requests, userToken);
        // Sync subscription statuses for new requests using the Data Products API
        await SyncDataSubscriptionStatusesAsync(requests, userToken);

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

        var sourceName     = sourceInstitution.Name;
        var sourceTenant   = sourceInstitution.TenantId;
        var sourceWorkspace = ResolveSourceWorkspaceId(request.DataProduct);
        var sourceLakehouse = request.DataProduct.SourceLakehouseItemId;
        var recipientTenant = request.RequestingTenantId ?? requestingInstitution?.TenantId;
        var shareType = request.ShareType.ToString();

        // Validate which required fields are missing
        var missingFields = new List<string>();
        if (string.IsNullOrEmpty(sourceWorkspace)) missingFields.Add("Source Data Asset Fabric workspace ID");
        if (string.IsNullOrEmpty(sourceLakehouse))  missingFields.Add("Data Product SourceLakehouseItemId");
        if (string.IsNullOrEmpty(request.TargetFabricWorkspaceId)) missingFields.Add("Target Fabric Workspace ID");
        if (string.IsNullOrEmpty(request.TargetLakehouseItemId))   missingFields.Add("Target Lakehouse Item ID");
        if (request.ShareType == Core.Enums.ShareType.External && string.IsNullOrEmpty(recipientTenant))
            missingFields.Add("Recipient Tenant ID");

        bool isReadyForAutoFulfill = missingFields.Count == 0;

        List<string> steps;
        if (request.ShareType == Core.Enums.ShareType.Internal)
        {
            steps = new List<string>
            {
                "1. Open the Fabric portal (https://app.fabric.microsoft.com)",
                $"2. Navigate to source workspace: {sourceWorkspace ?? "(configure a linked Data Asset workspace ID or Fabric:SourceWorkspaceOverride)"}",
                $"3. Open source lakehouse item: {sourceLakehouse ?? "(configure SourceLakehouseItemId on data product)"}",
                $"4. Grant workspace Viewer or Contributor role to the user: {request.RequestingUserEmail}",
                "   (Both workspaces are in the same tenant — no External Data Share is required)",
                $"5. The user should create a OneLake shortcut in their lakehouse: {request.TargetLakehouseItemId ?? "(not specified)"}",
                $"   in workspace: {request.TargetFabricWorkspaceId ?? "(not specified)"}",
                $"   pointing to this lakehouse: workspaceId={sourceWorkspace ?? "?"}, itemId={sourceLakehouse ?? "?"}",
                "6. Return to this portal and mark the request as 'Fulfilled'"
            };
        }
        else
        {
            steps = new List<string>
            {
                "1. Open the Fabric portal (https://app.fabric.microsoft.com)",
                $"2. Navigate to source workspace: {sourceWorkspace ?? "(configure a linked Data Asset workspace ID or Fabric:SourceWorkspaceOverride)"}",
                $"3. Open source lakehouse item: {sourceLakehouse ?? "(configure SourceLakehouseItemId on data product)"}",
                "4. Click 'Share' → 'External data share'",
                $"5. Recipient tenant ID: {recipientTenant ?? "(unknown — requesting tenant ID not captured)"}",
                $"6. Recipient email: {request.RequestingUserEmail}",
                "7. Set appropriate permissions and confirm the share — copy the Share ID",
                $"8. Notify the recipient. They should accept the share and create a OneLake shortcut",
                $"   in workspace: {request.TargetFabricWorkspaceId ?? "(not specified)"}",
                $"   in lakehouse: {request.TargetLakehouseItemId ?? "(not specified)"}",
                "9. Return to this portal and mark the request as 'Fulfilled', providing the Share ID"
            };
        }

        return Ok(new FulfillmentDetailsDto(
            RequestId: request.Id,
            DataProductName: request.DataProduct.Name,
            ShareType: shareType,
            SourceInstitutionName: sourceName,
            SourceTenantId: sourceTenant,
            SourceFabricWorkspaceId: sourceWorkspace,
            SourceLakehouseItemId: sourceLakehouse,
            RecipientTenantId: recipientTenant,
            RecipientUserEmail: request.RequestingUserEmail,
            RecipientUserName: request.RequestingUserName,
            RequestingInstitutionName: requestingInstitution?.Name,
            TargetFabricWorkspaceId: request.TargetFabricWorkspaceId,
            TargetLakehouseItemId: request.TargetLakehouseItemId,
            FulfillmentSteps: steps,
            IsReadyForAutoFulfill: isReadyForAutoFulfill,
            MissingFields: missingFields
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
    /// <summary>
    /// Syncs subscription statuses for requests that use the Purview Data Products API
    /// (i.e. have an ExternalShareId but no PurviewWorkflowRunId) and are still pending.
    /// Maps subscriptionStatus → local RequestStatus and stores the raw Purview status
    /// in PurviewWorkflowStatus so the UI can display it.
    /// </summary>
    private async Task SyncDataSubscriptionStatusesAsync(IList<AccessRequest> requests, string? userToken)
    {
        var toSync = requests
            .Where(r => !string.IsNullOrEmpty(r.ExternalShareId)
                        && string.IsNullOrEmpty(r.PurviewWorkflowRunId))
            .ToList();

        if (toSync.Count == 0) return;

        foreach (var req in toSync)
        {
            try
            {
                var institution = req.DataProduct?.Institution;
                if (institution == null || string.IsNullOrEmpty(institution.TenantId))
                    continue;

                var result = await _dataAccessService.GetDataSubscriptionAsync(
                    institution.TenantId,
                    req.ExternalShareId!,
                    userToken);

                if (!result.Success || result.Subscription == null) continue;

                var purviewStatus = result.Subscription.Status;
                if (string.IsNullOrEmpty(purviewStatus)) continue;

                var changed = false;
                var canUpdateLocalStatus = req.Status == RequestStatus.Submitted || req.Status == RequestStatus.UnderReview;

                // Update the raw Purview status so the UI can display it
                if (req.PurviewWorkflowStatus != purviewStatus)
                {
                    req.PurviewWorkflowStatus = purviewStatus;
                    changed = true;
                }

                // Map Purview subscription status to local RequestStatus
                var isApproved  = purviewStatus.Equals("Active",    StringComparison.OrdinalIgnoreCase)
                               || purviewStatus.Equals("Approved",  StringComparison.OrdinalIgnoreCase);
                var isDenied    = purviewStatus.Equals("Denied",    StringComparison.OrdinalIgnoreCase)
                               || purviewStatus.Equals("Rejected",  StringComparison.OrdinalIgnoreCase);
                var isCancelled = purviewStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                               || purviewStatus.Equals("Canceled",  StringComparison.OrdinalIgnoreCase);
                var isUnderReview = purviewStatus.Equals("InReview",    StringComparison.OrdinalIgnoreCase)
                                 || purviewStatus.Equals("UnderReview", StringComparison.OrdinalIgnoreCase)
                                 || purviewStatus.Equals("Review",      StringComparison.OrdinalIgnoreCase);

                if (isApproved && canUpdateLocalStatus)
                {
                    req.Status = RequestStatus.Approved;
                    req.StatusChangedDate = DateTime.UtcNow;
                    req.StatusChangedBy = "PurviewSubscription";
                    changed = true;

                    var (fulfilled, fulfillError) = await TryAutoFulfillAsync(req);
                    if (!fulfilled)
                        _logger.LogWarning(
                            "Auto-fulfillment not completed for request {RequestId}: {Error}",
                            req.Id, fulfillError);
                }
                else if (isDenied && canUpdateLocalStatus)
                {
                    req.Status = RequestStatus.Denied;
                    req.StatusChangedDate = DateTime.UtcNow;
                    req.StatusChangedBy = "PurviewSubscription";
                    changed = true;
                }
                else if (isCancelled && canUpdateLocalStatus)
                {
                    req.Status = RequestStatus.Cancelled;
                    req.StatusChangedDate = DateTime.UtcNow;
                    req.StatusChangedBy = "PurviewSubscription";
                    changed = true;
                }
                else if (isUnderReview && req.Status == RequestStatus.Submitted)
                {
                    req.Status = RequestStatus.UnderReview;
                    req.StatusChangedDate = DateTime.UtcNow;
                    req.StatusChangedBy = "PurviewSubscription";
                    changed = true;
                }

                if (changed)
                {
                    await _requestRepo.UpdateAsync(req);
                    _logger.LogInformation(
                        "Synced Purview subscription status for request {RequestId}: " +
                        "subscriptionStatus={SubscriptionStatus}, localStatus={Status}",
                        req.Id, purviewStatus, req.Status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to sync Purview subscription status for request {RequestId} " +
                    "(subscriptionId={SubscriptionId}). Skipping.",
                    req.Id, req.ExternalShareId);
            }
        }
    }

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

                        // Attempt automated shortcut creation (gated by global + per-institution flags)
                        var (fulfilled, fulfillError) = await TryAutoFulfillAsync(req);
                        if (fulfilled)
                        {
                            _logger.LogInformation(
                                "Auto-fulfillment completed for request {RequestId}", req.Id);
                        }
                        else
                        {
                            // TryAutoFulfillAsync already logs detailed warnings/errors internally;
                            // log at Warning here so this surfaces in operational monitoring.
                            _logger.LogWarning(
                                "Auto-fulfillment not completed for request {RequestId}: {Error}",
                                req.Id, fulfillError);
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

            // System-level master switch
            var globalEnabled = _configuration.GetValue<bool>("Fabric:AutoFulfillOnApproval", false);
            if (!globalEnabled)
            {
                _logger.LogInformation("Auto-fulfillment skipped for request {RequestId}: global setting disabled", req.Id);
                return (false, "Auto-fulfillment is disabled globally (Fabric:AutoFulfillOnApproval)");
            }

            // Per-institution switch
            if (institution == null || !institution.AutoFulfillEnabled)
            {
                var msg = institution == null
                    ? "Source institution is missing"
                    : $"Auto-fulfillment not enabled for institution '{institution.Name}'";
                _logger.LogInformation("Auto-fulfillment skipped for request {RequestId}: {Reason}", req.Id, msg);
                return (false, msg);
            }

            // Validate all required fields are present for automated fulfillment.
            var sourceWorkspaceId = req.DataProduct == null ? null : ResolveSourceWorkspaceId(req.DataProduct);

            if (string.IsNullOrEmpty(sourceWorkspaceId))
            {
                var msg = "Source Data Asset Fabric workspace ID is missing";
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

            var sourceItemId = req.DataProduct?.SourceLakehouseItemId
                ?? _configuration["Fabric:SourceItemOverride"];

            if (string.IsNullOrEmpty(sourceItemId))
            {
                var msg = "No source lakehouse item ID configured on the Data Product. " +
                          "Set DataProduct.SourceLakehouseItemId or Fabric:SourceItemOverride app setting.";
                _logger.LogWarning("Cannot auto-fulfill request {RequestId}: {Reason}", req.Id, msg);
                return (false, msg);
            }

            AutoFulfillmentResult result;

            if (req.ShareType == Core.Enums.ShareType.Internal)
            {
                // Same-tenant: direct OneLake shortcut, no external data share required
                _logger.LogInformation(
                    "Auto-fulfillment (Internal) for request {RequestId}: sourceWorkspace={Workspace}, " +
                    "sourceLakehouse={ItemId}, tenant={Tenant}, targetWorkspace={TargetWs}, targetLakehouse={TargetLh}",
                    req.Id, sourceWorkspaceId, sourceItemId, institution.TenantId,
                    req.TargetFabricWorkspaceId, req.TargetLakehouseItemId);

                result = await _fabricShortcutService.CreateInternalShortcutAsync(
                    sourceWorkspaceId: sourceWorkspaceId,
                    sourceItemId: sourceItemId,
                    tenantId: institution.TenantId,
                    targetWorkspaceId: req.TargetFabricWorkspaceId,
                    targetLakehouseId: req.TargetLakehouseItemId,
                    dataProductName: req.DataProduct?.Name ?? "DataProduct",
                    cancellationToken: default);
            }
            else
            {
                // Cross-tenant: external data share + OneLake shortcut
                var recipientTenantId = req.RequestingTenantId ?? requestingInstitution?.TenantId;
                if (string.IsNullOrEmpty(recipientTenantId))
                {
                    var msg = $"Requesting tenant ID unknown (RequestingTenantId={req.RequestingTenantId ?? "(null)"}, " +
                              $"InstitutionTenantId={requestingInstitution?.TenantId ?? "(null)"})";
                    _logger.LogWarning("Cannot auto-fulfill request {RequestId}: {Reason}", req.Id, msg);
                    return (false, msg);
                }

                _logger.LogInformation(
                    "Auto-fulfillment (External) for request {RequestId}: sourceWorkspace={Workspace}, " +
                    "sourceLakehouse={ItemId}, recipientTenant={Tenant}, targetWorkspace={TargetWs}, " +
                    "targetLakehouse={TargetLh}",
                    req.Id, sourceWorkspaceId, sourceItemId, recipientTenantId,
                    req.TargetFabricWorkspaceId, req.TargetLakehouseItemId);

                result = await _fabricShortcutService.CreateCrossTenantShortcutAsync(
                    sourceWorkspaceId: sourceWorkspaceId,
                    sourceItemId: sourceItemId,
                    sourceTenantId: institution.TenantId,
                    recipientTenantId: recipientTenantId,
                    recipientUserEmail: req.RequestingUserEmail,
                    targetWorkspaceId: req.TargetFabricWorkspaceId,
                    targetLakehouseId: req.TargetLakehouseItemId,
                    dataProductName: req.DataProduct?.Name ?? "DataProduct",
                    cancellationToken: default);
            }

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
                        ? $"Your shortcut '{result.ShortcutName}' has been created automatically."
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
                        ShareType = req.ShareType.ToString(),
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

    private string? GetCurrentUserId()
    {
        return User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
    }

    private string? GetCurrentEntraObjectId()
    {
        var objectId = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        return Guid.TryParse(objectId, out _) ? objectId : null;
    }

    private static IReadOnlyList<string> ResolvePreferredDataAssetGuids(DataProduct product)
    {
        var institutionAccount = product.Institution?.PurviewAccountName;

        return product.DataProductDataAssets
            .Select(link => link.DataAsset)
            .Where(asset =>
                asset != null
                && !string.IsNullOrWhiteSpace(asset.PurviewAssetId)
                && (string.IsNullOrWhiteSpace(institutionAccount)
                    || string.IsNullOrWhiteSpace(asset.AccountName)
                    || asset.AccountName.Equals(institutionAccount, StringComparison.OrdinalIgnoreCase)))
            .Select(asset => asset!.PurviewAssetId)
            .ToList();
    }

    private static string BuildPurviewBusinessJustification(AccessRequest request, DataProduct product)
    {
        var isExternal = request.ShareType == Core.Enums.ShareType.External;
        var manualAction = isExternal
            ? "Manual action requested: create External Data Share to the requesting tenant/user. If feasible, create OneLake shortcut in the provided target workspace/lakehouse."
            : "Manual action requested: create/validate direct Fabric shortcut access in the provided target workspace/lakehouse (same-tenant request).";

        var lines = new List<string>
        {
            "Original Justification:",
            request.BusinessJustification,
            string.Empty,
            "Fulfillment Context:",
            $"- RequestId: {request.Id}",
            $"- ShareType: {request.ShareType}",
            $"- Requesting User: {request.RequestingUserName} ({request.RequestingUserEmail})",
            $"- Requesting Institution: {request.RequestingInstitution?.Name ?? request.RequestingTenantId ?? "(unknown)"}",
            $"- Data Product: {product.Name}",
            $"- Target WorkspaceId: {request.TargetFabricWorkspaceId ?? "(not provided)"}",
            $"- Target Lakehouse ItemId: {request.TargetLakehouseItemId ?? "(not provided)"}",
            $"- Requested Duration Days: {request.RequestedDurationDays?.ToString() ?? "(unspecified)"}",
            string.Empty,
            "Manual Fulfillment Guidance:",
            $"- {manualAction}"
        };

        return string.Join("<br />\n", lines);
    }

    private static AccessRequestDto MapToDto(AccessRequest r) => new(
        r.Id,
        r.DataProductId,
        r.DataProduct?.Name ?? "Unknown",
        r.DataProduct?.Institution?.Name ?? "Unknown",
        r.DataProduct?.Institution?.PurviewAccountName,
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
        r.CreatedDate,
        r.ShareType.ToString()
    );
}
