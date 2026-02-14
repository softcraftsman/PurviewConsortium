using PurviewConsortium.Core.Enums;

namespace PurviewConsortium.Api.DTOs;

// --- Institution DTOs ---

public record InstitutionDto(
    Guid Id,
    string Name,
    string TenantId,
    string PurviewAccountName,
    string? FabricWorkspaceId,
    string PrimaryContactEmail,
    bool IsActive,
    bool AdminConsentGranted,
    DateTime CreatedDate,
    DateTime ModifiedDate);

public record CreateInstitutionDto(
    string Name,
    string TenantId,
    string PurviewAccountName,
    string? FabricWorkspaceId,
    string PrimaryContactEmail);

public record UpdateInstitutionDto(
    string Name,
    string PurviewAccountName,
    string? FabricWorkspaceId,
    string PrimaryContactEmail,
    bool IsActive,
    bool AdminConsentGranted);

// --- Data Product DTOs ---

public record DataProductListDto(
    Guid Id,
    string Name,
    string? Description,
    string? Owner,
    string? SourceSystem,
    string? SensitivityLabel,
    List<string> Classifications,
    List<string> GlossaryTerms,
    Guid InstitutionId,
    string InstitutionName,
    DateTime? PurviewLastModified);

public record DataProductDetailDto(
    Guid Id,
    string PurviewQualifiedName,
    string Name,
    string? Description,
    string? Owner,
    string? OwnerEmail,
    string? SourceSystem,
    string? SchemaJson,
    List<string> Classifications,
    List<string> GlossaryTerms,
    string? SensitivityLabel,
    Guid InstitutionId,
    string InstitutionName,
    string InstitutionContactEmail,
    DateTime? PurviewLastModified,
    DateTime? LastSyncedFromPurview,
    DateTime CreatedDate,
    AccessRequestStatusDto? CurrentUserRequest);

public record AccessRequestStatusDto(
    Guid RequestId,
    RequestStatus Status,
    DateTime CreatedDate);

// --- Access Request DTOs ---

public record CreateAccessRequestDto(
    Guid DataProductId,
    string? TargetFabricWorkspaceId,
    string? TargetLakehouseName,
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
    Guid RequestingInstitutionId,
    string RequestingInstitutionName,
    string? TargetFabricWorkspaceId,
    string? TargetLakehouseName,
    string BusinessJustification,
    int? RequestedDurationDays,
    RequestStatus Status,
    DateTime? StatusChangedDate,
    string? StatusChangedBy,
    string? ExternalShareId,
    DateTime? ExpirationDate,
    DateTime CreatedDate);

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

public record FulfillmentDetailsDto(
    Guid RequestId,
    string DataProductName,
    string SourceInstitutionName,
    string? SourceFabricWorkspaceId,
    string RecipientTenantId,
    string RecipientUserEmail,
    string? TargetFabricWorkspaceId,
    string? TargetLakehouseName,
    List<string> FulfillmentSteps);
