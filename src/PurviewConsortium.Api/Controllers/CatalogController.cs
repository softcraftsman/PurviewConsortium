using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PurviewConsortium.Api.DTOs;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Interfaces;
using PurviewConsortium.Infrastructure.Data;

namespace PurviewConsortium.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogSearchService _searchService;
    private readonly IDataProductRepository _dataProductRepo;
    private readonly IDataAssetRepository _dataAssetRepo;
    private readonly IInstitutionRepository _institutionRepo;
    private readonly IAccessRequestRepository _accessRequestRepo;
    private readonly ConsortiumDbContext _dbContext;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(
        ICatalogSearchService searchService,
        IDataProductRepository dataProductRepo,
        IDataAssetRepository dataAssetRepo,
        IInstitutionRepository institutionRepo,
        IAccessRequestRepository accessRequestRepo,
        ConsortiumDbContext dbContext,
        ILogger<CatalogController> logger)
    {
        _searchService = searchService;
        _dataProductRepo = dataProductRepo;
        _dataAssetRepo = dataAssetRepo;
        _institutionRepo = institutionRepo;
        _accessRequestRepo = accessRequestRepo;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>Search and list Data Products in the catalog.</summary>
    [HttpGet("products")]
    public async Task<ActionResult<CatalogSearchResponseDto>> SearchProducts(
        [FromQuery] string? search,
        [FromQuery] string? institutions,
        [FromQuery] string? classifications,
        [FromQuery] string? sensitivityLabels,
        [FromQuery] string? glossaryTerms,
        [FromQuery] string? sourceSystem,
        [FromQuery] DateTime? updatedAfter,
        [FromQuery] DateTime? updatedBefore,
        [FromQuery] string sortBy = "relevance",
        [FromQuery] string sortDirection = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var request = new CatalogSearchRequest
        {
            SearchText = search,
            InstitutionIds = ParseGuidList(institutions),
            Classifications = ParseStringList(classifications),
            SensitivityLabels = ParseStringList(sensitivityLabels),
            GlossaryTerms = ParseStringList(glossaryTerms),
            SourceSystem = sourceSystem,
            UpdatedAfter = updatedAfter,
            UpdatedBefore = updatedBefore,
            SortBy = sortBy,
            SortDirection = sortDirection,
            Page = page,
            PageSize = pageSize
        };

        var result = await _searchService.SearchAsync(request);

        return Ok(new CatalogSearchResponseDto(
            Items: result.Items.Select(i => new DataProductListDto(
                i.Id, i.Name, i.Description, i.Owner, i.SourceSystem,
                i.SensitivityLabel, i.Classifications,
                i.InstitutionId, i.InstitutionName, i.PurviewLastModified, i.AssetCount
            )).ToList(),
            TotalCount: result.TotalCount,
            Facets: result.Facets.ToDictionary(
                f => f.Key,
                f => f.Value.Select(v => new FacetValueDto(v.Value, v.Count)).ToList()
            )
        ));
    }

    /// <summary>Get full details for a specific Data Product.</summary>
    [HttpGet("products/{id:guid}")]
    public async Task<ActionResult<DataProductDetailDto>> GetProduct(Guid id)
    {
        var product = await _dataProductRepo.GetByIdAsync(id);
        if (product == null || !product.IsListed)
            return NotFound();

        var userId = GetCurrentUserId();
        AccessRequestStatusDto? currentRequest = null;

        if (userId != null)
        {
            var existing = await _accessRequestRepo.GetActiveRequestAsync(userId, id);
            if (existing != null)
            {
                currentRequest = new AccessRequestStatusDto(existing.Id, existing.Status, existing.CreatedDate);
            }
        }

        var linkedAssets = await GetLinkedDataAssetsAsync(id);
        var ownerContacts = BuildOwnerContacts(product);
        var assetNameByPurviewId = linkedAssets.ToDictionary(a => a.PurviewAssetId, a => a.Name, StringComparer.OrdinalIgnoreCase);
        var termsOfUse = BuildLinkDtos(product.GetTermsOfUseLinks(), assetNameByPurviewId, product.TermsOfUseUrl, "Terms of Use");
        var documentation = BuildLinkDtos(product.GetDocumentationLinks(), assetNameByPurviewId, product.DocumentationUrl, "Documentation");
        var dataAssets = linkedAssets.Select(asset => new DataAssetListItemDto(
            asset.Id,
            asset.PurviewAssetId,
            asset.Name,
            asset.Type,
            asset.Description,
            asset.AssetType,
            asset.FullyQualifiedName,
            asset.AccountName,
            asset.LastRefreshedAt,
            asset.PurviewCreatedAt,
            asset.PurviewLastModifiedAt,
            asset.InstitutionId,
            asset.Institution.Name,
            termsOfUse.Where(link => string.Equals(link.DataAssetId, asset.PurviewAssetId, StringComparison.OrdinalIgnoreCase)).ToList(),
            documentation.Where(link => string.Equals(link.DataAssetId, asset.PurviewAssetId, StringComparison.OrdinalIgnoreCase)).ToList()
        )).ToList();

        return Ok(new DataProductDetailDto(
            product.Id,
            product.PurviewQualifiedName,
            product.Name,
            product.Description,
            ownerContacts,
            product.SchemaJson,
            product.GetClassifications(),
            product.InstitutionId,
            product.Institution.Name,
            product.Institution.TenantId,
            product.PurviewLastModified,
            product.LastSyncedFromPurview,
            product.CreatedDate,
            currentRequest,
            product.AssetCount,
            product.BusinessUse,
            product.UseCases,
            product.DataQualityScore,
            product.UpdateFrequency,
            product.TermsOfUseUrl,
            termsOfUse,
            product.DocumentationUrl,
            documentation,
            dataAssets
        ));
    }

    /// <summary>Get catalog statistics for the dashboard.</summary>
    [AllowAnonymous]
    [HttpGet("stats")]
    public async Task<ActionResult<CatalogStatsDto>> GetStats()
    {
        var userId = GetCurrentUserId();
        int totalProducts;
        List<Institution> institutions;
        Dictionary<Guid, int> countsByInstitution;

        try
        {
            totalProducts = await _dataProductRepo.GetTotalCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog stats failed while loading total product count.");
            totalProducts = 0;
        }

        try
        {
            institutions = await _institutionRepo.GetAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog stats failed while loading institutions.");
            institutions = new List<Institution>();
        }

        try
        {
            countsByInstitution = await _dataProductRepo.GetCountByInstitutionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog stats failed while loading product counts by institution.");
            countsByInstitution = new Dictionary<Guid, int>();
        }

        int pendingRequests = 0, activeShares = 0;
        if (userId != null)
        {
            try
            {
                pendingRequests = await _accessRequestRepo.GetPendingCountByUserAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Catalog stats failed while loading pending request count for user {UserId}.", userId);
            }

            try
            {
                activeShares = await _accessRequestRepo.GetActiveCountByUserAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Catalog stats failed while loading active share count for user {UserId}.", userId);
            }
        }

        var institutionLookup = institutions.ToDictionary(i => i.Id, i => i.Name);
        var productsByInstitution = countsByInstitution
            .Where(kv => institutionLookup.ContainsKey(kv.Key))
            .ToDictionary(kv => institutionLookup[kv.Key], kv => kv.Value);

        return Ok(new CatalogStatsDto(
            TotalProducts: totalProducts,
            TotalInstitutions: institutions.Count,
            UserPendingRequests: pendingRequests,
            UserActiveShares: activeShares,
            RecentAdditions: new List<DataProductListDto>(), // Populated via search
            ProductsByInstitution: productsByInstitution
        ));
    }

    /// <summary>List all Data Assets in the catalog.</summary>
    [HttpGet("data-assets")]
    public async Task<ActionResult<DataAssetListResponseDto>> GetDataAssets(
        [FromQuery] string? search,
        [FromQuery] string? assetType,
        [FromQuery] string? institution)
    {
        var assets = await _dataAssetRepo.GetAllAsync();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            assets = assets.Where(a =>
                a.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (a.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.WorkspaceName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        if (!string.IsNullOrWhiteSpace(assetType))
        {
            assets = assets.Where(a =>
                string.Equals(a.AssetType, assetType, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        if (Guid.TryParse(institution, out var instId))
        {
            assets = assets.Where(a => a.InstitutionId == instId).ToList();
        }

        var items = assets.Select(a => new DataAssetListItemDto(
            a.Id,
            a.PurviewAssetId,
            a.Name,
            a.Type,
            a.Description,
            a.AssetType,
            a.FullyQualifiedName,
            a.AccountName,
            a.LastRefreshedAt,
            a.PurviewCreatedAt,
            a.PurviewLastModifiedAt,
            a.InstitutionId,
            a.Institution.Name,
            new List<DataProductLinkDto>(),
            new List<DataProductLinkDto>()
        )).ToList();

        return Ok(new DataAssetListResponseDto(items, items.Count));
    }

    /// <summary>Get available filter values for the catalog.</summary>
    [HttpGet("filters")]
    public async Task<ActionResult<CatalogFiltersDto>> GetFilters()
    {
        var institutions = await _institutionRepo.GetAllAsync();

        // Get distinct values from a simple search
        var searchResult = await _searchService.SearchAsync(new CatalogSearchRequest
        {
            SearchText = "*",
            Page = 1,
            PageSize = 1
        });

        return Ok(new CatalogFiltersDto(
            Institutions: institutions.Select(i => new InstitutionFilterDto(i.Id, i.Name)).ToList(),
            Classifications: searchResult.Facets.GetValueOrDefault("classifications")?.Select(f => f.Value).ToList() ?? new(),
            GlossaryTerms: searchResult.Facets.GetValueOrDefault("glossaryTerms")?.Select(f => f.Value).ToList() ?? new(),
            SensitivityLabels: searchResult.Facets.GetValueOrDefault("sensitivityLabel")?.Select(f => f.Value).ToList() ?? new(),
            SourceSystems: searchResult.Facets.GetValueOrDefault("sourceSystem")?.Select(f => f.Value).ToList() ?? new()
        ));
    }

    /// <summary>Get linked data assets for a data product from the join table.</summary>
    private async Task<List<DataAsset>> GetLinkedDataAssetsAsync(Guid dataProductId)
    {
        return await _dbContext.DataProductDataAssets
            .Where(link => link.DataProductId == dataProductId)
            .Include(link => link.DataAsset)
                .ThenInclude(a => a.Institution)
            .Select(link => link.DataAsset)
            .ToListAsync();
    }

    private static List<DataProductOwnerContactDto> BuildOwnerContacts(DataProduct product)
    {
        var contacts = product.GetOwnerContacts()
            .Select(contact => new DataProductOwnerContactDto(
                contact.Id,
                contact.Description,
                contact.Name,
                contact.EmailAddress))
            .ToList();

        if (contacts.Count == 0 && (!string.IsNullOrWhiteSpace(product.Owner) || !string.IsNullOrWhiteSpace(product.OwnerEmail)))
        {
            contacts.Add(new DataProductOwnerContactDto(
                Id: null,
                Description: null,
                Name: product.Owner,
                EmailAddress: product.OwnerEmail));
        }

        return contacts;
    }

    private static List<DataProductLinkDto> BuildLinkDtos(
        List<DataProductLinkInfo> links,
        IReadOnlyDictionary<string, string> assetNameByPurviewId,
        string? fallbackUrl,
        string fallbackName)
    {
        var mapped = links
            .Select(link => new DataProductLinkDto(
                link.DataAssetId,
                ResolveAssetName(link.DataAssetId, assetNameByPurviewId),
                link.Name,
                link.Url))
            .ToList();

        if (mapped.Count == 0 && !string.IsNullOrWhiteSpace(fallbackUrl))
        {
            mapped.Add(new DataProductLinkDto(
                DataAssetId: null,
                DataAssetName: "Unmapped asset",
                Name: fallbackName,
                Url: fallbackUrl));
        }

        return mapped;
    }

    private static string ResolveAssetName(string? dataAssetId, IReadOnlyDictionary<string, string> assetNameByPurviewId)
    {
        if (!string.IsNullOrWhiteSpace(dataAssetId) && assetNameByPurviewId.TryGetValue(dataAssetId, out var assetName))
            return assetName;

        return dataAssetId ?? "Unmapped asset";
    }

    private string? GetCurrentUserId() =>
        User.FindFirst("oid")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private static List<Guid>? ParseGuidList(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null :
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => Guid.TryParse(s, out _))
            .Select(Guid.Parse)
            .ToList();

    private static List<string>? ParseStringList(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null :
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
