using Microsoft.Extensions.Logging;
using PurviewConsortium.Core.Entities;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Infrastructure.Services;

/// <summary>
/// In-memory catalog search service for local development.
/// Queries the SQL database directly instead of Azure AI Search.
/// </summary>
public class InMemoryCatalogSearchService : ICatalogSearchService
{
    private readonly IDataProductRepository _dataProductRepo;
    private readonly IInstitutionRepository _institutionRepo;
    private readonly ILogger<InMemoryCatalogSearchService> _logger;

    public InMemoryCatalogSearchService(
        IDataProductRepository dataProductRepo,
        IInstitutionRepository institutionRepo,
        ILogger<InMemoryCatalogSearchService> logger)
    {
        _dataProductRepo = dataProductRepo;
        _institutionRepo = institutionRepo;
        _logger = logger;
    }

    public async Task<CatalogSearchResult> SearchAsync(CatalogSearchRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("InMemory search: query={Query}", request.SearchText);

        var institutions = await _institutionRepo.GetAllAsync(activeOnly: false);
        var allProducts = new List<DataProduct>();

        foreach (var inst in institutions)
        {
            var products = await _dataProductRepo.GetByInstitutionAsync(inst.Id, listedOnly: true);
            foreach (var p in products)
            {
                p.Institution = inst; // ensure nav property is set
            }
            allProducts.AddRange(products);
        }

        // Simple text search
        if (!string.IsNullOrWhiteSpace(request.SearchText) && request.SearchText != "*")
        {
            var q = request.SearchText.ToLowerInvariant();
            allProducts = allProducts.Where(p =>
                (p.Name?.ToLowerInvariant().Contains(q) ?? false) ||
                (p.Description?.ToLowerInvariant().Contains(q) ?? false) ||
                (p.Owner?.ToLowerInvariant().Contains(q) ?? false) ||
                (p.SourceSystem?.ToLowerInvariant().Contains(q) ?? false)
            ).ToList();
        }

        // Filters
        if (request.InstitutionIds is { Count: > 0 })
            allProducts = allProducts.Where(p => request.InstitutionIds.Contains(p.InstitutionId)).ToList();

        if (request.Classifications is { Count: > 0 })
            allProducts = allProducts.Where(p =>
                p.GetClassifications().Any(c => request.Classifications.Contains(c))).ToList();

        if (request.SensitivityLabels is { Count: > 0 })
            allProducts = allProducts.Where(p =>
                request.SensitivityLabels.Contains(p.SensitivityLabel ?? "")).ToList();

        if (request.GlossaryTerms is { Count: > 0 })
            allProducts = allProducts.Where(p =>
                p.GetGlossaryTerms().Any(t => request.GlossaryTerms.Contains(t))).ToList();

        if (!string.IsNullOrEmpty(request.SourceSystem))
            allProducts = allProducts.Where(p => p.SourceSystem == request.SourceSystem).ToList();

        // Build facets
        var facets = new Dictionary<string, List<FacetValue>>();

        facets["institutionName"] = allProducts
            .GroupBy(p => p.Institution?.Name ?? "Unknown")
            .Select(g => new FacetValue { Value = g.Key, Count = g.Count() })
            .ToList();

        facets["classifications"] = allProducts
            .SelectMany(p => p.GetClassifications())
            .GroupBy(c => c)
            .Select(g => new FacetValue { Value = g.Key, Count = g.Count() })
            .ToList();

        facets["sensitivityLabel"] = allProducts
            .Where(p => !string.IsNullOrEmpty(p.SensitivityLabel))
            .GroupBy(p => p.SensitivityLabel!)
            .Select(g => new FacetValue { Value = g.Key, Count = g.Count() })
            .ToList();

        facets["glossaryTerms"] = allProducts
            .SelectMany(p => p.GetGlossaryTerms())
            .GroupBy(t => t)
            .Select(g => new FacetValue { Value = g.Key, Count = g.Count() })
            .ToList();

        facets["sourceSystem"] = allProducts
            .Where(p => !string.IsNullOrEmpty(p.SourceSystem))
            .GroupBy(p => p.SourceSystem!)
            .Select(g => new FacetValue { Value = g.Key, Count = g.Count() })
            .ToList();

        var totalCount = allProducts.Count;

        // Pagination
        var paged = allProducts
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new CatalogSearchResult
        {
            TotalCount = totalCount,
            Facets = facets,
            Items = paged.Select(p => new CatalogSearchItem
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Owner = p.Owner,
                SourceSystem = p.SourceSystem,
                SensitivityLabel = p.SensitivityLabel,
                Classifications = p.GetClassifications(),
                GlossaryTerms = p.GetGlossaryTerms(),
                InstitutionId = p.InstitutionId,
                InstitutionName = p.Institution?.Name ?? "Unknown",
                PurviewLastModified = p.PurviewLastModified,
                AssetCount = p.AssetCount
            }).ToList()
        };
    }

    public Task IndexDataProductAsync(Guid dataProductId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[InMemory] IndexDataProduct called for {Id} — no-op in dev mode", dataProductId);
        return Task.CompletedTask;
    }

    public Task RemoveFromIndexAsync(Guid dataProductId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[InMemory] RemoveFromIndex called for {Id} — no-op in dev mode", dataProductId);
        return Task.CompletedTask;
    }

    public Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[InMemory] RebuildIndex called — no-op in dev mode");
        return Task.CompletedTask;
    }
}
