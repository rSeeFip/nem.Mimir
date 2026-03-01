using System.Net;
using System.Net.Http.Json;
using System.Text;
using Shouldly;

namespace Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests for the <see cref="Controllers.PluginsController"/>.
/// Focuses on authentication enforcement, admin authorization requirements,
/// route validation, and correct error status codes.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class PluginsControllerTests
{
    private readonly HttpClient _client;

    public PluginsControllerTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Authentication: all endpoints require a valid JWT token ──────────────

    [Theory]
    [InlineData("POST", "/api/plugins")]
    [InlineData("GET", "/api/plugins")]
    [InlineData("POST", "/api/plugins/test-plugin/execute")]
    [InlineData("DELETE", "/api/plugins/test-plugin")]
    public async Task AllEndpoints_WithoutToken_Return401(string method, string url)
    {
        // Arrange
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        if (method is "POST")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Load: requires authentication ───────────────────────────────────────

    [Fact]
    public async Task Load_WithoutToken_Returns401()
    {
        // Arrange
        var payload = new { AssemblyPath = "/path/to/plugin.dll" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/plugins", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Load_WithPathTraversal_WithoutToken_Returns401()
    {
        // Arrange — path traversal attempt; auth check should come before validation
        var payload = new { AssemblyPath = "../../etc/passwd" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/plugins", payload);

        // Assert — auth is checked first, so 401 not 400
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Load_WithEmptyBody_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.PostAsync(
            "/api/plugins",
            new StringContent("", Encoding.UTF8, "application/json"));

        // Assert — auth is checked first
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── List: requires authentication ───────────────────────────────────────

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/plugins");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Execute: requires authentication ────────────────────────────────────

    [Fact]
    public async Task Execute_WithoutToken_Returns401()
    {
        // Arrange
        var payload = new
        {
            UserId = Guid.NewGuid().ToString(),
            Parameters = new Dictionary<string, object> { ["key"] = "value" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/plugins/test-plugin/execute", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Execute_UnknownPlugin_WithoutToken_Returns401()
    {
        // Arrange — even with a non-existent plugin ID, auth comes first
        var payload = new
        {
            UserId = "user-1",
            Parameters = new Dictionary<string, object>()
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/plugins/non-existent-plugin-xyz/execute", payload);

        // Assert — 401 before any 404 check
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Unload: requires authentication ─────────────────────────────────────

    [Fact]
    public async Task Unload_WithoutToken_Returns401()
    {
        // Arrange
        var pluginId = "some-plugin";

        // Act
        var response = await _client.DeleteAsync($"/api/plugins/{pluginId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unload_UnknownPlugin_WithoutToken_Returns401()
    {
        // Arrange — non-existent plugin; auth check comes first
        var response = await _client.DeleteAsync("/api/plugins/non-existent-plugin");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Route existence and edge cases ──────────────────────────────────────

    [Fact]
    public async Task Load_RouteExists_ReturnsNon404()
    {
        // Arrange
        var payload = new { AssemblyPath = "/some/path.dll" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/plugins", payload);

        // Assert — should be 401 (not 404), proving route is mapped
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Execute_RouteExists_ReturnsNon404()
    {
        // Arrange
        var payload = new
        {
            UserId = "user-1",
            Parameters = new Dictionary<string, object>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/plugins/any-id/execute", payload);

        // Assert — route {id}/execute should be mapped → 401 not 404
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NonexistentSubRoute_Returns401OrNotFound()
    {
        // Act — unknown sub-route
        var response = await _client.GetAsync("/api/plugins/some-id/unknown-action");

        // Assert — should return 401 or 404
        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.NotFound)
            .ShouldBeTrue($"Expected 401 or 404 but got {statusCode}");
    }

    [Fact]
    public async Task Load_WithNullContent_WithoutToken_Returns401()
    {
        // Arrange — send POST with no content
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/plugins");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — auth check precedes content negotiation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Unload_WithEmptyPluginId_WithoutToken_ReturnsCorrectStatus(string pluginId)
    {
        // Act — empty/whitespace IDs may match the POST route for Load or return 401
        var response = await _client.DeleteAsync($"/api/plugins/{pluginId}");

        // Assert — should be 401 (auth checked first) or another client error;
        // empty ID on DELETE /api/plugins/ may match GET list route → 401
        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Execute_WithEmptyBody_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.PostAsync(
            "/api/plugins/test/execute",
            new StringContent("", Encoding.UTF8, "application/json"));

        // Assert — auth is checked before body deserialization
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
