using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PurviewConsortium.Infrastructure.Services;

namespace PurviewConsortium.Tests;

/// <summary>
/// Unit tests for <see cref="PurviewScannerService"/> covering:
/// <list type="bullet">
///   <item>Governance domain GUID → name resolution via the domains API</item>
///   <item>Separate tracking of Terms of Use and Documentation asset IDs</item>
///   <item>Direct URL extraction when Purview returns a string instead of an array</item>
/// </list>
/// </summary>
public class PurviewScannerServiceTests
{
    private const string TenantId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string PurviewAccount = "my-purview";
    private const string SalesDomainId = "dddddddd-dddd-dddd-dddd-dddddddddddd";
    private const string SalesDomainName = "Sales Domain";
    private const string TermsAssetId = "asset-terms-001";
    private const string DocsAssetId = "asset-docs-002";

    private static TestablePurviewScannerService CreateService(MockPurviewHttpHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:ClientId"] = "fake-client",
                ["AzureAd:ClientSecret"] = "fake-secret",
                ["ASPNETCORE_ENVIRONMENT"] = "Production"
            })
            .Build();

        return new TestablePurviewScannerService(mockFactory.Object, config, Mock.Of<ILogger<PurviewScannerService>>());
    }

    // ── Domain GUID resolution ──────────────────────────────────────────────

    [Fact]
    public async Task ScanDataProducts_DomainReturnedAsGuid_ResolvedToNameFromDomainsApi()
    {
        // Arrange: Purview returns the domain as a plain GUID string; domains API maps it to a name
        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(BuildDomainsJson(SalesDomainId, SalesDomainName));
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-001",
            name = "Sales Data",
            governanceDomain = SalesDomainId  // plain GUID — no name
        }));

        var svc = CreateService(handler);

        // Act
        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        // Assert
        Assert.Single(results);
        Assert.Equal(SalesDomainName, results[0].SourceSystem);
        Assert.Equal(SalesDomainName, results[0].GovernanceDomain);
    }

    [Fact]
    public async Task ScanDataProducts_DomainReturnedAsObject_NameExtractedDirectly()
    {
        // Arrange: Purview returns the domain as an object with id and name — no domains API lookup needed
        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(BuildDomainsJson(SalesDomainId, SalesDomainName));
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-002",
            name = "Sales Data 2",
            governanceDomain = new { id = SalesDomainId, name = SalesDomainName }
        }));

        var svc = CreateService(handler);

        // Act
        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        // Assert
        Assert.Single(results);
        Assert.Equal(SalesDomainName, results[0].SourceSystem);
    }

    [Fact]
    public async Task ScanDataProducts_DomainGuidNotInDomainsApi_GuidRetained()
    {
        // Arrange: Purview returns a GUID that does not appear in the domains list
        const string unknownDomainId = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";
        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(BuildDomainsJson(SalesDomainId, SalesDomainName));
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-003",
            name = "Unknown Domain Product",
            governanceDomain = unknownDomainId
        }));

        var svc = CreateService(handler);

        // Act
        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        // Assert — unknown GUID is kept as-is because we have no name for it
        Assert.Single(results);
        Assert.Equal(unknownDomainId, results[0].SourceSystem);
    }

    [Fact]
    public async Task ScanDataProducts_DomainsApiFails_ScanStillSucceeds()
    {
        // Arrange: domains API returns 500; scan should still return data products
        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(HttpStatusCode.InternalServerError, "{}");
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-004",
            name = "Resilient Product",
            governanceDomain = SalesDomainId
        }));

        var svc = CreateService(handler);

        // Act — should not throw
        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        // Assert — product returned, domain is the raw GUID since lookup failed
        Assert.Single(results);
        Assert.Equal(SalesDomainId, results[0].SourceSystem);
    }

    // ── Terms of Use / Documentation asset ID tracking ─────────────────────

    [Fact]
    public async Task ScanDataProducts_TermsOfUseAndDocumentationAsArrays_AssetIdsTrackedSeparately()
    {
        // Arrange: termsOfUse and documentation are arrays of {dataAssetId} references
        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(BuildDomainsJson(SalesDomainId, SalesDomainName));
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-005",
            name = "Linked Product",
            termsOfUse = new[] { new { dataAssetId = TermsAssetId } },
            documentation = new[] { new { dataAssetId = DocsAssetId } }
        }));

        var svc = CreateService(handler);

        // Act
        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        // Assert — separate lists are populated
        Assert.Single(results);
        Assert.Contains(TermsAssetId, results[0].TermsOfUseAssetIds);
        Assert.Contains(DocsAssetId, results[0].DocumentationAssetIds);
        // Combined list also contains both
        Assert.Contains(TermsAssetId, results[0].LinkedPurviewAssetIds);
        Assert.Contains(DocsAssetId, results[0].LinkedPurviewAssetIds);
    }

    [Fact]
    public async Task ScanDataProducts_TermsOfUseArray_DocumentationUrlIsNull()
    {
        // Arrange: termsOfUse is an array (not a direct URL), so TermsOfUseUrl should remain null here
        // (URL resolution happens later in SyncOrchestrator after asset data is available)
        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(BuildDomainsJson(SalesDomainId, SalesDomainName));
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-006",
            name = "Array Links Product",
            termsOfUse = new[] { new { dataAssetId = TermsAssetId } },
            documentation = new[] { new { dataAssetId = DocsAssetId } }
        }));

        var svc = CreateService(handler);

        // Act
        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        // Assert — array form means StrOrObj won't extract a URL; resolved later
        Assert.Single(results);
        Assert.Null(results[0].TermsOfUseUrl);
        Assert.Null(results[0].DocumentationUrl);
    }

    [Fact]
    public async Task ScanDataProducts_TermsOfUseAndDocumentationArrays_WithNamedLinks_AreCaptured()
    {
        const string termsUrl = "https://aka.ms/randomlink";
        const string docsUrl = "https://aka.ms/randomdocs";

        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(BuildDomainsJson(SalesDomainId, SalesDomainName));
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-006b",
            name = "Named Links Product",
            termsOfUse = new[]
            {
                new { dataAssetId = TermsAssetId, name = "Terms Packet", url = termsUrl }
            },
            documentation = new[]
            {
                new { dataAssetId = DocsAssetId, name = "Runbook", url = docsUrl }
            }
        }));

        var svc = CreateService(handler);

        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        Assert.Single(results);
        Assert.Equal(termsUrl, results[0].TermsOfUseUrl);
        Assert.Equal(docsUrl, results[0].DocumentationUrl);
        Assert.Single(results[0].TermsOfUseLinks);
        Assert.Equal("Terms Packet", results[0].TermsOfUseLinks[0].Name);
        Assert.Equal(TermsAssetId, results[0].TermsOfUseLinks[0].DataAssetId);
        Assert.Single(results[0].DocumentationLinks);
        Assert.Equal("Runbook", results[0].DocumentationLinks[0].Name);
        Assert.Equal(DocsAssetId, results[0].DocumentationLinks[0].DataAssetId);
    }

    [Fact]
    public async Task ScanDataProducts_TermsOfUseAsString_UrlCapturedInTermsOfUseUrl()
    {
        // Arrange: termsOfUse is a plain URL string (not an array)
        const string termsUrl = "https://example.com/terms";
        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(BuildDomainsJson(SalesDomainId, SalesDomainName));
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-007",
            name = "Direct URL Product",
            termsOfUse = termsUrl
        }));

        var svc = CreateService(handler);

        // Act
        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        // Assert — direct URL is captured immediately
        Assert.Single(results);
        Assert.Equal(termsUrl, results[0].TermsOfUseUrl);
        Assert.Empty(results[0].TermsOfUseAssetIds);
    }

    [Fact]
    public async Task ScanDataProducts_DocumentationAsDirectUrl_UrlCapturedInDocumentationUrl()
    {
        // Arrange: documentation is provided via the "documentationUrl" property
        const string docsUrl = "https://example.com/docs";
        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(BuildDomainsJson(SalesDomainId, SalesDomainName));
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-008",
            name = "Direct Docs Product",
            documentationUrl = docsUrl
        }));

        var svc = CreateService(handler);

        // Act
        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        // Assert
        Assert.Single(results);
        Assert.Equal(docsUrl, results[0].DocumentationUrl);
        Assert.Empty(results[0].DocumentationAssetIds);
    }

    [Fact]
    public async Task ScanDataProducts_NoTermsOrDocumentation_ListsAreEmpty()
    {
        // Arrange: product has no terms/documentation at all
        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(BuildDomainsJson(SalesDomainId, SalesDomainName));
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-009",
            name = "Minimal Product"
        }));

        var svc = CreateService(handler);

        // Act
        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        // Assert
        Assert.Single(results);
        Assert.Empty(results[0].TermsOfUseAssetIds);
        Assert.Empty(results[0].DocumentationAssetIds);
        Assert.Null(results[0].TermsOfUseUrl);
        Assert.Null(results[0].DocumentationUrl);
    }

    [Fact]
    public async Task ScanDataProducts_ContactsOwnerShape_OwnerContactsCaptured()
    {
        const string ownerId = "4bafb39b-fbd3-42c4-8e20-1c8e485f23c5";

        var handler = new MockPurviewHttpHandler();
        handler.SetDomainsResponse(BuildDomainsJson(SalesDomainId, SalesDomainName));
        handler.SetDataProductsResponse(BuildDataProductsJson(new
        {
            id = "prod-010",
            name = "Owner Contact Product",
            contacts = new
            {
                owner = new[]
                {
                    new { id = ownerId, description = "Creator" }
                }
            }
        }));

        var svc = CreateService(handler);

        var results = await svc.ScanForShareableDataProductsAsync(PurviewAccount, TenantId);

        Assert.Single(results);
        Assert.Equal(ownerId, results[0].OwnerObjectId);
        Assert.Single(results[0].OwnerContacts);
        Assert.Equal(ownerId, results[0].OwnerContacts[0].Id);
        Assert.Equal("Creator", results[0].OwnerContacts[0].Description);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string BuildDomainsJson(string id, string name)
    {
        var domain = new { id, name };
        return JsonSerializer.Serialize(new { value = new[] { domain } });
    }

    private static string BuildDataProductsJson(object product)
    {
        return JsonSerializer.Serialize(new { value = new[] { product } });
    }
}

/// <summary>
/// Subclass that skips Azure Identity token acquisition during tests.
/// </summary>
internal class TestablePurviewScannerService : PurviewScannerService
{
    public TestablePurviewScannerService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PurviewScannerService> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    protected override Task<string> GetAccessTokenAsync(string tenantId, CancellationToken cancellationToken)
        => Task.FromResult($"fake-token-for-{tenantId}");
}

/// <summary>
/// Routes HTTP requests to configurable mock responses based on URL path.
/// </summary>
internal class MockPurviewHttpHandler : HttpMessageHandler
{
    private HttpStatusCode _domainsStatusCode = HttpStatusCode.OK;
    private string _domainsResponseBody = """{"value":[]}""";

    private HttpStatusCode _productsStatusCode = HttpStatusCode.OK;
    private string _productsResponseBody = """{"value":[]}""";

    public void SetDomainsResponse(string json)
        => _domainsResponseBody = json;

    public void SetDomainsResponse(HttpStatusCode status, string json)
    {
        _domainsStatusCode = status;
        _domainsResponseBody = json;
    }

    public void SetDataProductsResponse(string json)
        => _productsResponseBody = json;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";

        if (url.Contains("/domains"))
        {
            return Task.FromResult(new HttpResponseMessage(_domainsStatusCode)
            {
                Content = new StringContent(_domainsResponseBody, Encoding.UTF8, "application/json")
            });
        }

        if (url.Contains("/dataProducts") || url.Contains("/datagovernance/catalog"))
        {
            return Task.FromResult(new HttpResponseMessage(_productsStatusCode)
            {
                Content = new StringContent(_productsResponseBody, Encoding.UTF8, "application/json")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("not found", Encoding.UTF8, "application/json")
        });
    }
}
