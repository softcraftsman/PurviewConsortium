using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurviewConsortium.Api.DTOs;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly ICatalogSearchService _searchService;
    private readonly IDataProductRepository _dataProductRepo;
    private readonly IInstitutionRepository _institutionRepo;
    private readonly IAccessRequestRepository _accessRequestRepo;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(
        ICatalogSearchService searchService,
        IDataProductRepository dataProductRepo,
        IInstitutionRepository institutionRepo,
        IAccessRequestRepository accessRequestRepo,
        ILogger<CatalogController> logger)
    {
        _searchService = searchService;
        _dataProductRepo = dataProductRepo;
        _institutionRepo = institutionRepo;
        _accessRequestRepo = accessRequestRepo;
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
                i.SensitivityLabel, i.Classifications, i.GlossaryTerms,
                i.InstitutionId, i.InstitutionName, i.PurviewLastModified
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

        return Ok(new DataProductDetailDto(
            product.Id,
            product.PurviewQualifiedName,
            product.Name,
            product.Description,
            product.Owner,
            product.OwnerEmail,
            product.SourceSystem,
            product.SchemaJson,
            product.GetClassifications(),
            product.GetGlossaryTerms(),
            product.SensitivityLabel,
            product.InstitutionId,
            product.Institution.Name,
            product.Institution.PrimaryContactEmail,
            product.PurviewLastModified,
            product.LastSyncedFromPurview,
            product.CreatedDate,
            currentRequest
        ));
    }

    /// <summary>Get catalog statistics for the dashboard.</summary>
    [AllowAnonymous]
    [HttpGet("stats")]
    public async Task<ActionResult<CatalogStatsDto>> GetStats()
    {
        var userId = GetCurrentUserId();
        var totalProducts = await _dataProductRepo.GetTotalCountAsync();
        var institutions = await _institutionRepo.GetAllAsync();
        var countsByInstitution = await _dataProductRepo.GetCountByInstitutionAsync();

        int pendingRequests = 0, activeShares = 0;
        if (userId != null)
        {
            pendingRequests = await _accessRequestRepo.GetPendingCountByUserAsync(userId);
            activeShares = await _accessRequestRepo.GetActiveCountByUserAsync(userId);
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
