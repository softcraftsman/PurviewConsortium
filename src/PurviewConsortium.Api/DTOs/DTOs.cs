using PurviewConsortium.Core.Enums;

namespace PurviewConsortium.Api.DTOs;

// --- Institution DTOs ---

public record InstitutionDto(
    Guid Id,
    string Name,
    string TenantId,
    string PurviewAccountName,
    string? FabricWorkspaceId,
    string? ConsortiumDomainIds,
    string PrimaryContactEmail,
    bool IsActive,
    bool AdminConsentGranted,
    bool AutoFulfillEnabled,
    DateTime CreatedDate,
    DateTime ModifiedDate);

public record CreateInstitutionDto(
    string Name,
    string TenantId,
    string PurviewAccountName,
    string? FabricWorkspaceId,
    string? ConsortiumDomainIds,
    string PrimaryContactEmail);

public record UpdateInstitutionDto(
    string Name,
    string PurviewAccountName,
    string? FabricWorkspaceId,
    string? ConsortiumDomainIds,
    string PrimaryContactEmail,
    bool IsActive,
    bool AdminConsentGranted,
    bool AutoFulfillEnabled);

// --- Data Product DTOs ---

public record DataProductListDto(
    Guid Id,
    string Name,
    string? Description,
    string? Owner,
    string? SourceSystem,
    string? SensitivityLabel,
    List<string> Classifications,
    Guid InstitutionId,
    string InstitutionName,
    DateTime? PurviewLastModified,
    int AssetCount);

public record DataProductDetailDto(
    Guid Id,
    string PurviewQualifiedName,
    string Name,
    string? Description,
    List<DataProductOwnerContactDto> OwnerContacts,
    string? SchemaJson,
    List<string> Classifications,
    Guid InstitutionId,
    string InstitutionName,
    string InstitutionTenantId,
    DateTime? PurviewLastModified,
    DateTime? LastSyncedFromPurview,
    DateTime CreatedDate,
    AccessRequestStatusDto? CurrentUserRequest,
    int AssetCount,
    string? BusinessUse,
    string? UseCases,
    int? DataQualityScore,
    string? UpdateFrequency,
    string? TermsOfUseUrl,
    List<DataProductLinkDto> TermsOfUse,
    string? DocumentationUrl,
    List<DataProductLinkDto> Documentation,
    List<DataAssetListItemDto> DataAssets);

public record DataProductOwnerContactDto(
    string? Id,
    string? Description,
    string? Name,
    string? EmailAddress);

public record DataProductLinkDto(
    string? DataAssetId,
    string DataAssetName,
    string? Name,
    string? Url);

// --- Data Asset List DTOs ---

public record DataAssetListItemDto(
    Guid Id,
    string PurviewAssetId,
    string Name,
    string? Type,
    string? Description,
    string? AssetType,
    string? FullyQualifiedName,
    string? AccountName,
    DateTime? LastRefreshedAt,
    DateTime? PurviewCreatedAt,
    DateTime? PurviewLastModifiedAt,
    Guid InstitutionId,
    string InstitutionName,
    List<DataProductLinkDto> TermsOfUse,
    List<DataProductLinkDto> Documentation);

public record DataAssetListResponseDto(
    List<DataAssetListItemDto> Items,
    int TotalCount);

public record AccessRequestStatusDto(
    Guid RequestId,
    RequestStatus Status,
    DateTime CreatedDate);

// --- Access Request DTOs ---

public record CreateAccessRequestDto(
    Guid DataProductId,
    string? TargetFabricWorkspaceId,
    string? TargetLakehouseItemId,
    string BusinessJustification,
    int? RequestedDurationDays);

public record AccessRequestDto(
    Guid Id,
    Guid DataProductId,
    string DataProductName,
    string OwningInstitutionName,
    string RequestingUserId,
    string RequestingUserEmail,
    string RequestingUserName,
    Guid? RequestingInstitutionId,
    string? RequestingInstitutionName,
    string? TargetFabricWorkspaceId,
    string? TargetLakehouseItemId,
    string BusinessJustification,
    int? RequestedDurationDays,
    RequestStatus Status,
    DateTime? StatusChangedDate,
    string? StatusChangedBy,
    string? ExternalShareId,
    string? FabricShortcutName,
    bool FabricShortcutCreated,
    string? PurviewWorkflowRunId,
    string? PurviewWorkflowStatus,
    DateTime? ExpirationDate,
    DateTime CreatedDate,
    string ShareType,
    string? SourceFabricWorkspaceId,
    string? SourceLakehouseItemId,
    string? SourceTenantId,
    string? SourceInstitutionName);

public record UpdateRequestStatusDto(
    RequestStatus NewStatus,
    string? Comment,
    string? ExternalShareId);

// --- Catalog Search DTOs ---

public record CatalogSearchResponseDto(
    List<DataProductListDto> Items,
    int TotalCount,
    Dictionary<string, List<FacetValueDto>> Facets);

public record FacetValueDto(string Value, long Count);

// --- Stats DTOs ---

public record CatalogStatsDto(
    int TotalProducts,
    int TotalInstitutions,
    int UserPendingRequests,
    int UserActiveShares,
    List<DataProductListDto> RecentAdditions,
    Dictionary<string, int> ProductsByInstitution);

// --- Filter DTOs ---

public record CatalogFiltersDto(
    List<InstitutionFilterDto> Institutions,
    List<string> Classifications,
    List<string> GlossaryTerms,
    List<string> SensitivityLabels,
    List<string> SourceSystems);

public record InstitutionFilterDto(Guid Id, string Name);

// --- Sync DTOs ---

public record SyncHistoryDto(
    Guid Id,
    Guid InstitutionId,
    string InstitutionName,
    DateTime StartTime,
    DateTime? EndTime,
    string Status,
    int ProductsFound,
    int ProductsAdded,
    int ProductsUpdated,
    int ProductsDelisted,
    string? ErrorDetails);

// --- Fulfillment DTOs ---

// --- Audit Log DTOs ---

public record AuditLogDto(
    Guid Id,
    DateTime Timestamp,
    string? UserId,
    string? UserEmail,
    string Action,
    string? EntityType,
    string? EntityId,
    string? DetailsJson,
    string? IpAddress);

// --- Fulfillment DTOs ---

public record FulfillmentDetailsDto(
    Guid RequestId,
    string DataProductName,
    string ShareType,
    // Source
    string SourceInstitutionName,
    string? SourceTenantId,
    string? SourceFabricWorkspaceId,
    string? SourceLakehouseItemId,
    // Recipient
    string? RecipientTenantId,
    string RecipientUserEmail,
    string RecipientUserName,
    string? RequestingInstitutionName,
    // Target
    string? TargetFabricWorkspaceId,
    string? TargetLakehouseItemId,
    // Instructions
    List<string> FulfillmentSteps,
    bool IsReadyForAutoFulfill,
    List<string> MissingFields);

// --- Admin DTOs ---
