namespace PurviewConsortium.Core.Interfaces;

public interface ICatalogSearchService
{
    Task<CatalogSearchResult> SearchAsync(CatalogSearchRequest request, CancellationToken cancellationToken = default);
    Task IndexDataProductAsync(Guid dataProductId, CancellationToken cancellationToken = default);
    Task RemoveFromIndexAsync(Guid dataProductId, CancellationToken cancellationToken = default);
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);
}

public class CatalogSearchRequest
{
    public string? SearchText { get; set; }
    public List<Guid>? InstitutionIds { get; set; }
    public List<string>? Classifications { get; set; }
    public List<string>? SensitivityLabels { get; set; }
    public List<string>? GlossaryTerms { get; set; }
    public string? SourceSystem { get; set; }
    public DateTime? UpdatedAfter { get; set; }
    public DateTime? UpdatedBefore { get; set; }
    public string SortBy { get; set; } = "relevance";
    public string SortDirection { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class CatalogSearchResult
{
    public List<CatalogSearchItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public Dictionary<string, List<FacetValue>> Facets { get; set; } = new();
}

public class CatalogSearchItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Owner { get; set; }
    public string? SourceSystem { get; set; }
    public string? SensitivityLabel { get; set; }
    public List<string> Classifications { get; set; } = new();
    public List<string> GlossaryTerms { get; set; } = new();
    public Guid InstitutionId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public DateTime? PurviewLastModified { get; set; }
    public double? SearchScore { get; set; }
}

public class FacetValue
{
    public string Value { get; set; } = string.Empty;
    public long Count { get; set; }
}
