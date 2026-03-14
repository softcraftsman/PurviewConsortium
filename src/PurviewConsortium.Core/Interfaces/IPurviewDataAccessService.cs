namespace PurviewConsortium.Core.Interfaces;

/// <summary>
/// A single data subscription returned by the Purview Data Access API.
/// </summary>
public class DataSubscriptionItem
{
    public string Id { get; set; } = string.Empty;
    public string DataProductId { get; set; } = string.Empty;
    public string SubscriberObjectId { get; set; } = string.Empty;
    public string IdentityType { get; set; } = string.Empty;
    public string? BusinessJustification { get; set; }
    public string? UseCase { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

/// <summary>
/// Result of creating or updating a data subscription.
/// </summary>
public class CreateDataSubscriptionResult
{
    public bool Success { get; set; }
    public string? SubscriptionId { get; set; }
    public string? ErrorMessage { get; set; }
    public DataSubscriptionItem? Subscription { get; set; }
}

/// <summary>
/// Result of listing data subscriptions.
/// </summary>
public class ListDataSubscriptionsResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DataSubscriptionItem> Subscriptions { get; set; } = new();
}

/// <summary>
/// Manages Purview Data Access subscriptions via the
/// datagovernance/dataaccess API (purview-service.microsoft.com endpoint).
/// </summary>
public interface IPurviewDataAccessService
{
    /// <summary>
    /// Creates or updates a data subscription in Purview for the given data product and subscriber.
    /// The caller supplies a new GUID as the subscription ID (PUT semantics).
    /// </summary>
    /// <param name="tenantId">
    /// The owning institution's Azure AD tenant ID, which is also used as the
    /// prefix of the {tenantId}-api.purview-service.microsoft.com hostname.
    /// </param>
    /// <param name="subscriptionId">A new GUID to use as the subscription's resource ID.</param>
    /// <param name="dataProductId">The Purview data product ID (GUID string).</param>
    /// <param name="subscriberObjectId">The Azure AD object ID of the subscriber.</param>
    /// <param name="identityType">Identity type: "User", "ServicePrincipal", or "Group".</param>
    /// <param name="businessJustification">Why the subscriber needs access.</param>
    /// <param name="purpose">The named purpose value required by the data product's policy set.</param>
    /// <param name="userAccessToken">Optional caller bearer token for OBO token acquisition.</param>
    Task<CreateDataSubscriptionResult> CreateDataSubscriptionAsync(
        string tenantId,
        string subscriptionId,
        string dataProductId,
        string subscriberObjectId,
        string identityType,
        string businessJustification,
        string purpose,
        string? userAccessToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists data subscriptions from Purview, optionally filtered by subscriber object ID
    /// and/or data product ID.
    /// </summary>
    /// <param name="tenantId">
    /// The owning institution's Azure AD tenant ID, which is also used as the
    /// prefix of the {tenantId}-api.purview-service.microsoft.com hostname.
    /// </param>
    /// <param name="subscriberObjectId">When provided, only subscriptions for this Azure AD object ID are returned.</param>
    /// <param name="dataProductId">When provided, only subscriptions for this data product are returned.</param>
    /// <param name="userAccessToken">Optional caller bearer token for OBO token acquisition.</param>
    Task<ListDataSubscriptionsResult> ListUserDataSubscriptionsAsync(
        string tenantId,
        string? subscriberObjectId = null,
        string? dataProductId = null,
        string? userAccessToken = null,
        CancellationToken cancellationToken = default);
}
