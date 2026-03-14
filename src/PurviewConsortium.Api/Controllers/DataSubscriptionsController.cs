using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurviewConsortium.Api.DTOs;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Api.Controllers;

/// <summary>
/// Manages Purview Data Access subscriptions.
///
/// These endpoints proxy the Purview datagovernance/dataaccess REST API,
/// allowing callers to create subscriptions and list subscriptions for a user.
/// </summary>
[ApiController]
[Route("api/datasubscriptions")]
[Authorize]
public class DataSubscriptionsController : ControllerBase
{
    private readonly IPurviewDataAccessService _dataAccessService;
    private readonly ILogger<DataSubscriptionsController> _logger;

    public DataSubscriptionsController(
        IPurviewDataAccessService dataAccessService,
        ILogger<DataSubscriptionsController> logger)
    {
        _dataAccessService = dataAccessService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new Purview data subscription for the specified data product and subscriber.
    /// A new GUID is generated server-side as the subscription ID (PUT to Purview).
    /// </summary>
    /// <remarks>
    /// Calls:
    ///   PUT https://{tenantId}-api.purview-service.microsoft.com/datagovernance/dataaccess/dataSubscriptions/{newGuid}
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<CreateDataSubscriptionResponseDto>> CreateSubscription(
        [FromBody] CreateDataSubscriptionDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userToken = ExtractBearerToken();
        var subscriptionId = Guid.NewGuid().ToString();

        var result = await _dataAccessService.CreateDataSubscriptionAsync(
            tenantId: dto.TenantId,
            subscriptionId: subscriptionId,
            dataProductId: dto.DataProductId,
            subscriberObjectId: dto.SubscriberObjectId,
            identityType: dto.IdentityType,
            businessJustification: dto.BusinessJustification,
            purpose: dto.UseCase,
            userAccessToken: userToken,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Failed to create data subscription for data product {DataProductId}: {Error}",
                dto.DataProductId, result.ErrorMessage);

            return BadRequest(new CreateDataSubscriptionResponseDto(
                false, null, null, result.ErrorMessage));
        }

        var subscriptionDto = result.Subscription != null
            ? MapToDto(result.Subscription)
            : null;

        return Ok(new CreateDataSubscriptionResponseDto(
            true, result.SubscriptionId, subscriptionDto, null));
    }

    /// <summary>
    /// Lists Purview data subscriptions, optionally filtered by subscriber object ID and/or data product ID.
    /// </summary>
    /// <remarks>
    /// Calls:
    ///   GET https://{tenantId}-api.purview-service.microsoft.com/datagovernance/dataaccess/dataSubscriptions
    /// then filters client-side by the provided parameters.
    /// </remarks>
    /// <param name="tenantId">The institution's Azure AD tenant ID.</param>
    /// <param name="subscriberObjectId">Optional. Return only subscriptions for this Azure AD object ID.</param>
    /// <param name="dataProductId">Optional. Return only subscriptions for this data product.</param>
    [HttpGet]
    public async Task<ActionResult<ListDataSubscriptionsResponseDto>> ListSubscriptions(
        [FromQuery] string tenantId,
        [FromQuery] string? subscriberObjectId = null,
        [FromQuery] string? dataProductId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest("tenantId is required.");

        var userToken = ExtractBearerToken();

        var result = await _dataAccessService.ListUserDataSubscriptionsAsync(
            tenantId: tenantId,
            subscriberObjectId: subscriberObjectId,
            dataProductId: dataProductId,
            userAccessToken: userToken,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Failed to list data subscriptions for tenant {TenantId}: {Error}",
                tenantId, result.ErrorMessage);

            return BadRequest(new ListDataSubscriptionsResponseDto(
                false, new List<DataSubscriptionDto>(), result.ErrorMessage));
        }

        var dtos = result.Subscriptions.Select(MapToDto).ToList();

        return Ok(new ListDataSubscriptionsResponseDto(true, dtos, null));
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private string? ExtractBearerToken() =>
        HttpContext.Request.Headers["Authorization"]
            .FirstOrDefault()
            ?.Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);

    private static DataSubscriptionDto MapToDto(DataSubscriptionItem item) => new(
        item.Id,
        item.DataProductId,
        item.SubscriberObjectId,
        item.IdentityType,
        item.BusinessJustification,
        item.UseCase,
        item.Status,
        item.CreatedDate,
        item.ModifiedDate);
}
