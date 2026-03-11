using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PurviewConsortium.Infrastructure.Services;

namespace PurviewConsortium.Tests;

/// <summary>
/// Unit tests for <see cref="FabricShortcutService"/> validating the external data share
/// and OneLake shortcut creation flow against mocked Fabric REST API responses.
/// </summary>
public class FabricShortcutServiceTests
{
    // Common test GUIDs
    private const string SourceWorkspaceId = "11111111-1111-1111-1111-111111111111";
    private const string SourceItemId = "22222222-2222-2222-2222-222222222222";
    private const string SourceTenantId = "33333333-3333-3333-3333-333333333333";
    private const string RecipientTenantId = "44444444-4444-4444-4444-444444444444";
    private const string RecipientEmail = "consumer@contoso.com";
    private const string TargetWorkspaceId = "55555555-5555-5555-5555-555555555555";
    private const string TargetLakehouseId = "66666666-6666-6666-6666-666666666666";
    private const string DataProductName = "Sales Analytics";
    private const string MockShareId = "share-99999999";

    /// <summary>
    /// Creates a <see cref="FabricShortcutService"/> whose HTTP calls are intercepted
    /// by the supplied handler, bypassing real Azure Identity token acquisition.
    /// </summary>
    private static FabricShortcutService CreateService(
        MockFabricHttpHandler handler,
        ILogger<FabricShortcutService>? logger = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.fabric.microsoft.com") };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient("Fabric")).Returns(httpClient);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:ClientId"] = "fake-client-id",
                ["AzureAd:ClientSecret"] = "fake-client-secret"
            })
            .Build();

        logger ??= Mock.Of<ILogger<FabricShortcutService>>();

        return new TestableFabricShortcutService(mockFactory.Object, config, logger);
    }

    #region CreateCrossTenantShortcutAsync — Full Success

    [Fact]
    public async Task CreateCrossTenantShortcut_BothStepsSucceed_ReturnsSuccess()
    {
        // Arrange — both Fabric API calls return 201
        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { id = MockShareId }));
        handler.SetShortcutResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { name = "Sales_Analytics" }));

        var svc = CreateService(handler);

        // Act
        var result = await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, DataProductName);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.PartialSuccess);
        Assert.Equal(MockShareId, result.ExternalShareId);
        Assert.Equal("Sales_Analytics", result.ShortcutName);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CreateCrossTenantShortcut_BothStepsSucceed_SendsCorrectSharePayload()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { id = MockShareId }));
        handler.SetShortcutResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { name = "Sales_Analytics" }));

        var svc = CreateService(handler);
        await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, DataProductName);

        // Verify the external share request URL and payload
        var shareReq = handler.CapturedRequests
            .First(r => r.Url.Contains("externalDataShares"));
        Assert.Contains($"/workspaces/{SourceWorkspaceId}/items/{SourceItemId}/externalDataShares", shareReq.Url);
        Assert.Equal("POST", shareReq.Method);

        using var doc = JsonDocument.Parse(shareReq.Body!);
        var recipient = doc.RootElement.GetProperty("recipient");
        Assert.Equal(RecipientTenantId, recipient.GetProperty("tenantId").GetString());
        Assert.Equal(RecipientEmail, recipient.GetProperty("userPrincipalName").GetString());
    }

    [Fact]
    public async Task CreateCrossTenantShortcut_BothStepsSucceed_SendsCorrectShortcutPayload()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { id = MockShareId }));
        handler.SetShortcutResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { name = "Sales_Analytics" }));

        var svc = CreateService(handler);
        await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, DataProductName);

        // Verify the shortcut request URL and payload
        var shortcutReq = handler.CapturedRequests
            .First(r => r.Url.Contains("shortcuts"));
        Assert.Contains($"/workspaces/{TargetWorkspaceId}/items/{TargetLakehouseId}/shortcuts", shortcutReq.Url);

        using var doc = JsonDocument.Parse(shortcutReq.Body!);
        Assert.Equal("Sales_Analytics", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("Tables/Sales_Analytics", doc.RootElement.GetProperty("path").GetString());

        var oneLake = doc.RootElement.GetProperty("target").GetProperty("oneLake");
        Assert.Equal(SourceWorkspaceId, oneLake.GetProperty("workspaceId").GetString());
        Assert.Equal(SourceItemId, oneLake.GetProperty("itemId").GetString());
    }

    #endregion

    #region CreateCrossTenantShortcutAsync — Share Creation Failures

    [Fact]
    public async Task CreateCrossTenantShortcut_ShareApiFails403_ReturnsFailure()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareResponse(HttpStatusCode.Forbidden,
            "{\"error\":\"Insufficient permissions\"}");

        var svc = CreateService(handler);
        var result = await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, DataProductName);

        Assert.False(result.Success);
        Assert.False(result.PartialSuccess);
        Assert.Null(result.ExternalShareId);
        Assert.Contains("External data share creation failed", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateCrossTenantShortcut_ShareApiReturns500_ReturnsFailure()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareResponse(HttpStatusCode.InternalServerError,
            "{\"error\":\"Internal server error\"}");

        var svc = CreateService(handler);
        var result = await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, DataProductName);

        Assert.False(result.Success);
        Assert.False(result.PartialSuccess);
        Assert.Contains("Fabric API error", result.ErrorMessage);
    }

    #endregion

    #region CreateCrossTenantShortcutAsync — Partial Success (share OK, shortcut fails)

    [Fact]
    public async Task CreateCrossTenantShortcut_ShortcutApiFails_ReturnsPartialSuccess()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { id = MockShareId }));
        handler.SetShortcutResponse(HttpStatusCode.BadRequest,
            "{\"error\":\"Invalid shortcut target\"}");

        var svc = CreateService(handler);
        var result = await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, DataProductName);

        Assert.False(result.Success);
        Assert.True(result.PartialSuccess);
        Assert.Equal(MockShareId, result.ExternalShareId);
        Assert.Contains("shortcut creation failed", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateCrossTenantShortcut_ShortcutApiReturns409Conflict_ReturnsPartialSuccess()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { id = MockShareId }));
        handler.SetShortcutResponse(HttpStatusCode.Conflict,
            "{\"error\":\"Shortcut already exists\"}");

        var svc = CreateService(handler);
        var result = await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, DataProductName);

        Assert.False(result.Success);
        Assert.True(result.PartialSuccess);
        Assert.Equal(MockShareId, result.ExternalShareId);
    }

    #endregion

    #region CreateCrossTenantShortcutAsync — Exception Handling

    [Fact]
    public async Task CreateCrossTenantShortcut_HttpExceptionThrown_ReturnsGracefulFailure()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareException(new HttpRequestException("DNS resolution failed"));

        var svc = CreateService(handler);
        var result = await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, DataProductName);

        Assert.False(result.Success);
        Assert.False(result.PartialSuccess);
        Assert.Contains("Unexpected error", result.ErrorMessage);
    }

    #endregion

    #region RevokeExternalShareAsync

    [Fact]
    public async Task RevokeExternalShare_DeleteReturns200_ReturnsTrue()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetRevokeResponse(HttpStatusCode.OK, "");

        var svc = CreateService(handler);
        var result = await svc.RevokeExternalShareAsync(
            SourceWorkspaceId, SourceItemId, MockShareId, SourceTenantId);

        Assert.True(result);

        var revokeReq = handler.CapturedRequests.First(r => r.Method == "DELETE");
        Assert.Contains($"/externalDataShares/{MockShareId}", revokeReq.Url);
    }

    [Fact]
    public async Task RevokeExternalShare_DeleteReturns204_ReturnsTrue()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetRevokeResponse(HttpStatusCode.NoContent, "");

        var svc = CreateService(handler);
        var result = await svc.RevokeExternalShareAsync(
            SourceWorkspaceId, SourceItemId, MockShareId, SourceTenantId);

        Assert.True(result);
    }

    [Fact]
    public async Task RevokeExternalShare_DeleteReturns404_ReturnsFalse()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetRevokeResponse(HttpStatusCode.NotFound, "{\"error\":\"Share not found\"}");

        var svc = CreateService(handler);
        var result = await svc.RevokeExternalShareAsync(
            SourceWorkspaceId, SourceItemId, MockShareId, SourceTenantId);

        Assert.False(result);
    }

    [Fact]
    public async Task RevokeExternalShare_ExceptionThrown_ReturnsFalse()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetRevokeException(new HttpRequestException("Connection refused"));

        var svc = CreateService(handler);
        var result = await svc.RevokeExternalShareAsync(
            SourceWorkspaceId, SourceItemId, MockShareId, SourceTenantId);

        Assert.False(result);
    }

    #endregion

    #region Shortcut Name Sanitization

    [Theory]
    [InlineData("Sales Analytics", "Sales_Analytics")]
    [InlineData("my-data-product", "my_data_product")]
    [InlineData("Product  With   Multiple   Spaces", "Product_With_Multiple_Spaces")]
    [InlineData("Special!@#$%^&*()Chars", "SpecialChars")]
    [InlineData("Mix of-hyphens and spaces", "Mix_of_hyphens_and_spaces")]
    [InlineData("already_underscored", "already_underscored")]
    [InlineData("UPPERCASE", "UPPERCASE")]
    [InlineData("123_numeric", "123_numeric")]
    public async Task CreateCrossTenantShortcut_SanitizesShortcutName(
        string productName, string expectedShortcutName)
    {
        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { id = MockShareId }));
        handler.SetShortcutResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { name = expectedShortcutName }));

        var svc = CreateService(handler);
        var result = await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, productName);

        Assert.True(result.Success);

        // Verify the sanitized name was sent to the Fabric API
        var shortcutReq = handler.CapturedRequests.First(r => r.Url.Contains("shortcuts"));
        using var doc = JsonDocument.Parse(shortcutReq.Body!);
        Assert.Equal(expectedShortcutName, doc.RootElement.GetProperty("name").GetString());
        Assert.Equal($"Tables/{expectedShortcutName}", doc.RootElement.GetProperty("path").GetString());
    }

    [Fact]
    public async Task CreateCrossTenantShortcut_LongName_TruncatesTo128Chars()
    {
        var longName = new string('A', 200);
        var expectedName = new string('A', 128);

        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { id = MockShareId }));
        handler.SetShortcutResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { name = expectedName }));

        var svc = CreateService(handler);
        var result = await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, longName);

        Assert.True(result.Success);

        var shortcutReq = handler.CapturedRequests.First(r => r.Url.Contains("shortcuts"));
        using var doc = JsonDocument.Parse(shortcutReq.Body!);
        var sentName = doc.RootElement.GetProperty("name").GetString();
        Assert.Equal(128, sentName!.Length);
    }

    #endregion

    #region Authorization Headers

    [Fact]
    public async Task CreateCrossTenantShortcut_SendsBearerTokenOnBothRequests()
    {
        var handler = new MockFabricHttpHandler();
        handler.SetExternalShareResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { id = MockShareId }));
        handler.SetShortcutResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { name = "Test" }));

        var svc = CreateService(handler);
        await svc.CreateCrossTenantShortcutAsync(
            SourceWorkspaceId, SourceItemId, SourceTenantId,
            RecipientTenantId, RecipientEmail,
            TargetWorkspaceId, TargetLakehouseId, "Test");

        // Both requests should have Authorization: Bearer headers
        Assert.All(handler.CapturedRequests, req =>
            Assert.StartsWith("Bearer ", req.AuthorizationHeader));
    }

    #endregion
}

#region Test Helpers

/// <summary>
/// A testable subclass that bypasses Azure Identity token acquisition,
/// returning a fake bearer token instead of calling ClientSecretCredential.
/// </summary>
internal class TestableFabricShortcutService : FabricShortcutService
{
    public TestableFabricShortcutService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FabricShortcutService> logger)
        : base(httpClientFactory, configuration, logger)
    {
    }

    protected override Task<string> GetFabricTokenAsync(
        string tenantId, CancellationToken cancellationToken)
    {
        return Task.FromResult($"fake-token-for-{tenantId}");
    }
}

/// <summary>
/// Captures HTTP requests and returns configurable responses for each Fabric API endpoint.
/// </summary>
internal class MockFabricHttpHandler : HttpMessageHandler
{
    private HttpStatusCode _shareStatusCode = HttpStatusCode.Created;
    private string _shareResponseBody = "{}";
    private Exception? _shareException;

    private HttpStatusCode _shortcutStatusCode = HttpStatusCode.Created;
    private string _shortcutResponseBody = "{}";
    private Exception? _shortcutException;

    private HttpStatusCode _revokeStatusCode = HttpStatusCode.OK;
    private string _revokeResponseBody = "";
    private Exception? _revokeException;

    public List<CapturedRequest> CapturedRequests { get; } = new();

    public void SetExternalShareResponse(HttpStatusCode status, string body)
    {
        _shareStatusCode = status;
        _shareResponseBody = body;
    }

    public void SetExternalShareException(Exception ex) => _shareException = ex;

    public void SetShortcutResponse(HttpStatusCode status, string body)
    {
        _shortcutStatusCode = status;
        _shortcutResponseBody = body;
    }

    public void SetShortcutException(Exception ex) => _shortcutException = ex;

    public void SetRevokeResponse(HttpStatusCode status, string body)
    {
        _revokeStatusCode = status;
        _revokeResponseBody = body;
    }

    public void SetRevokeException(Exception ex) => _revokeException = ex;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content != null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : null;

        CapturedRequests.Add(new CapturedRequest
        {
            Method = request.Method.Method,
            Url = request.RequestUri!.ToString(),
            Body = body,
            AuthorizationHeader = request.Headers.Authorization?.ToString() ?? ""
        });

        var url = request.RequestUri!.ToString();

        // Route to the correct mock response based on URL pattern
        if (request.Method == HttpMethod.Delete && url.Contains("externalDataShares"))
        {
            if (_revokeException != null) throw _revokeException;
            return new HttpResponseMessage(_revokeStatusCode)
            {
                Content = new StringContent(_revokeResponseBody, Encoding.UTF8, "application/json")
            };
        }

        if (url.Contains("externalDataShares"))
        {
            if (_shareException != null) throw _shareException;
            return new HttpResponseMessage(_shareStatusCode)
            {
                Content = new StringContent(_shareResponseBody, Encoding.UTF8, "application/json")
            };
        }

        if (url.Contains("shortcuts"))
        {
            if (_shortcutException != null) throw _shortcutException;
            return new HttpResponseMessage(_shortcutStatusCode)
            {
                Content = new StringContent(_shortcutResponseBody, Encoding.UTF8, "application/json")
            };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

internal class CapturedRequest
{
    public string Method { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Body { get; set; }
    public string AuthorizationHeader { get; set; } = "";
}

#endregion
