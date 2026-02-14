using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PurviewConsortium.Core.Interfaces;

namespace PurviewConsortium.Infrastructure.Services;

public class AzureAISearchService : ICatalogSearchService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly IDataProductRepository _dataProductRepo;
    private readonly IInstitutionRepository _institutionRepo;
    private readonly ILogger<AzureAISearchService> _logger;
    private const string IndexName = "data-products";

    public AzureAISearchService(
        IConfiguration configuration,
        IDataProductRepository dataProductRepo,
        IInstitutionRepository institutionRepo,
        ILogger<AzureAISearchService> logger)
    {
        var endpoint = new Uri(configuration["AzureAISearch:Endpoint"]
            ?? throw new InvalidOperationException("AzureAISearch:Endpoint not configured"));
        var apiKey = configuration["AzureAISearch:ApiKey"];

        AzureKeyCredential credential = new(apiKey ?? throw new InvalidOperationException("AzureAISearch:ApiKey not configured"));

        _indexClient = new SearchIndexClient(endpoint, credential);
        _searchClient = new SearchClient(endpoint, IndexName, credential);
        _dataProductRepo = dataProductRepo;
        _institutionRepo = institutionRepo;
        _logger = logger;
    }

    public async Task<CatalogSearchResult> SearchAsync(CatalogSearchRequest request, CancellationToken cancellationToken = default)
    {
        var options = new SearchOptions
        {
            Size = request.PageSize,
            Skip = (request.Page - 1) * request.PageSize,
            IncludeTotalCount = true,
            QueryType = SearchQueryType.Simple
        };

        // Add select fields
        options.Select.Add("id");
        options.Select.Add("name");
        options.Select.Add("description");
        options.Select.Add("owner");
        options.Select.Add("sourceSystem");
        options.Select.Add("sensitivityLabel");
        options.Select.Add("classifications");
        options.Select.Add("glossaryTerms");
        options.Select.Add("institutionId");
        options.Select.Add("institutionName");
        options.Select.Add("purviewLastModified");

        // Add facets
        options.Facets.Add("institutionName,count:50");
        options.Facets.Add("classifications,count:50");
        options.Facets.Add("sensitivityLabel,count:20");
        options.Facets.Add("glossaryTerms,count:50");
        options.Facets.Add("sourceSystem,count:20");

        // Build filters
        var filters = new List<string> { "isListed eq true" };

        if (request.InstitutionIds?.Any() == true)
        {
            var idFilters = request.InstitutionIds.Select(id => $"institutionId eq '{id}'");
            filters.Add($"({string.Join(" or ", idFilters)})");
        }

        if (request.Classifications?.Any() == true)
        {
            foreach (var c in request.Classifications)
                filters.Add($"classifications/any(t: t eq '{c}')");
        }

        if (request.SensitivityLabels?.Any() == true)
        {
            var labelFilters = request.SensitivityLabels.Select(l => $"sensitivityLabel eq '{l}'");
            filters.Add($"({string.Join(" or ", labelFilters)})");
        }

        if (request.GlossaryTerms?.Any() == true)
        {
            foreach (var t in request.GlossaryTerms)
                filters.Add($"glossaryTerms/any(g: g eq '{t}')");
        }

        if (!string.IsNullOrEmpty(request.SourceSystem))
            filters.Add($"sourceSystem eq '{request.SourceSystem}'");

        if (request.UpdatedAfter.HasValue)
            filters.Add($"purviewLastModified ge {request.UpdatedAfter.Value:O}");

        if (request.UpdatedBefore.HasValue)
            filters.Add($"purviewLastModified le {request.UpdatedBefore.Value:O}");

        options.Filter = string.Join(" and ", filters);

        // Sorting
        options.OrderBy.Add(request.SortBy switch
        {
            "name" => request.SortDirection == "asc" ? "name asc" : "name desc",
            "institution" => request.SortDirection == "asc" ? "institutionName asc" : "institutionName desc",
            "updated" => request.SortDirection == "asc" ? "purviewLastModified asc" : "purviewLastModified desc",
            _ => "search.score() desc"
        });

        var searchText = string.IsNullOrWhiteSpace(request.SearchText) ? "*" : request.SearchText;
        var response = await _searchClient.SearchAsync<SearchDocument>(searchText, options, cancellationToken);

        var result = new CatalogSearchResult
        {
            TotalCount = (int)(response.Value.TotalCount ?? 0)
        };

        await foreach (var doc in response.Value.GetResultsAsync())
        {
            result.Items.Add(new CatalogSearchItem
            {
                Id = Guid.Parse(doc.Document["id"].ToString()!),
                Name = doc.Document.GetString("name") ?? "",
                Description = doc.Document.ContainsKey("description") ? doc.Document.GetString("description") : null,
                Owner = doc.Document.ContainsKey("owner") ? doc.Document.GetString("owner") : null,
                SourceSystem = doc.Document.ContainsKey("sourceSystem") ? doc.Document.GetString("sourceSystem") : null,
                SensitivityLabel = doc.Document.ContainsKey("sensitivityLabel") ? doc.Document.GetString("sensitivityLabel") : null,
                Classifications = doc.Document.ContainsKey("classifications")
                    ? ((IEnumerable<object>)doc.Document["classifications"]).Select(o => o.ToString()!).ToList()
                    : new List<string>(),
                GlossaryTerms = doc.Document.ContainsKey("glossaryTerms")
                    ? ((IEnumerable<object>)doc.Document["glossaryTerms"]).Select(o => o.ToString()!).ToList()
                    : new List<string>(),
                InstitutionId = doc.Document.ContainsKey("institutionId") ? Guid.Parse(doc.Document["institutionId"].ToString()!) : Guid.Empty,
                InstitutionName = doc.Document.ContainsKey("institutionName") ? doc.Document.GetString("institutionName") ?? "" : "",
                PurviewLastModified = doc.Document.ContainsKey("purviewLastModified")
                    ? doc.Document.GetDateTimeOffset("purviewLastModified")?.UtcDateTime
                    : null,
                SearchScore = doc.Score
            });
        }

        // Map facets
        if (response.Value.Facets != null)
        {
            foreach (var facet in response.Value.Facets)
            {
                result.Facets[facet.Key] = facet.Value
                    .Select(f => new FacetValue { Value = f.Value.ToString()!, Count = f.Count ?? 0 })
                    .ToList();
            }
        }

        return result;
    }

    public async Task IndexDataProductAsync(Guid dataProductId, CancellationToken cancellationToken = default)
    {
        var product = await _dataProductRepo.GetByIdAsync(dataProductId);
        if (product == null) return;

        var doc = new SearchDocument
        {
            ["id"] = product.Id.ToString(),
            ["name"] = product.Name,
            ["description"] = product.Description ?? "",
            ["owner"] = product.Owner ?? "",
            ["sourceSystem"] = product.SourceSystem ?? "",
            ["sensitivityLabel"] = product.SensitivityLabel ?? "",
            ["classifications"] = product.GetClassifications(),
            ["glossaryTerms"] = product.GetGlossaryTerms(),
            ["institutionId"] = product.InstitutionId.ToString(),
            ["institutionName"] = product.Institution?.Name ?? "",
            ["isListed"] = product.IsListed,
            ["purviewLastModified"] = product.PurviewLastModified
        };

        await _searchClient.MergeOrUploadDocumentsAsync(new[] { doc }, cancellationToken: cancellationToken);
    }

    public async Task RemoveFromIndexAsync(Guid dataProductId, CancellationToken cancellationToken = default)
    {
        var doc = new SearchDocument { ["id"] = dataProductId.ToString() };
        await _searchClient.DeleteDocumentsAsync(new[] { doc }, cancellationToken: cancellationToken);
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rebuilding search index...");

        // Create or update the index schema
        await EnsureIndexExistsAsync(cancellationToken);

        // Re-index all listed products
        var institutions = await _institutionRepo.GetAllAsync(activeOnly: true);
        foreach (var institution in institutions)
        {
            var products = await _dataProductRepo.GetByInstitutionAsync(institution.Id, listedOnly: true);
            var batch = new List<SearchDocument>();

            foreach (var product in products)
            {
                batch.Add(new SearchDocument
                {
                    ["id"] = product.Id.ToString(),
                    ["name"] = product.Name,
                    ["description"] = product.Description ?? "",
                    ["owner"] = product.Owner ?? "",
                    ["sourceSystem"] = product.SourceSystem ?? "",
                    ["sensitivityLabel"] = product.SensitivityLabel ?? "",
                    ["classifications"] = product.GetClassifications(),
                    ["glossaryTerms"] = product.GetGlossaryTerms(),
                    ["institutionId"] = product.InstitutionId.ToString(),
                    ["institutionName"] = institution.Name,
                    ["isListed"] = product.IsListed,
                    ["purviewLastModified"] = product.PurviewLastModified
                });
            }

            if (batch.Any())
                await _searchClient.MergeOrUploadDocumentsAsync(batch, cancellationToken: cancellationToken);
        }

        _logger.LogInformation("Search index rebuild complete");
    }

    private async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        var index = new SearchIndex(IndexName)
        {
            Fields = new List<SearchField>
            {
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                new SearchableField("name") { IsFilterable = true, IsSortable = true },
                new SearchableField("description"),
                new SearchableField("owner") { IsFilterable = true },
                new SimpleField("sourceSystem", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
                new SimpleField("sensitivityLabel", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
                new SearchableField("classifications", collection: true) { IsFilterable = true, IsFacetable = true },
                new SearchableField("glossaryTerms", collection: true) { IsFilterable = true, IsFacetable = true },
                new SimpleField("institutionId", SearchFieldDataType.String) { IsFilterable = true },
                new SimpleField("institutionName", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true, IsSortable = true },
                new SimpleField("isListed", SearchFieldDataType.Boolean) { IsFilterable = true },
                new SimpleField("purviewLastModified", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true }
            }
        };

        await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
    }
}
