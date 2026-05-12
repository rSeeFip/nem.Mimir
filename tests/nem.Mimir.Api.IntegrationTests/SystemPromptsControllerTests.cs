using System.Net;
using System.Net.Http.Json;
using System.Text;
using Shouldly;

namespace nem.Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests for the <see cref="Controllers.SystemPromptsController"/>.
/// Focuses on authentication enforcement, route validation, and correct status codes.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SystemPromptsControllerTests
{
    private readonly HttpClient _client;

    public SystemPromptsControllerTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Authentication: all endpoints require a valid JWT token ──────────────

    [Theory]
    [InlineData("GET", "/api/systemprompts")]
    [InlineData("POST", "/api/systemprompts")]
    [InlineData("GET", "/api/systemprompts/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/systemprompts/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/systemprompts/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/systemprompts/00000000-0000-0000-0000-000000000001/render")]
    public async Task AllEndpoints_WithoutToken_Return401(string method, string url)
    {
        // Arrange
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        // Arrange
        var payload = new { Name = "Test Prompt", Template = "Hello {{name}}", Description = "Test" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/systemprompts", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/systemprompts");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/systemprompts/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_WithoutToken_Returns401()
    {
        // Arrange
        var id = Guid.NewGuid();
        var payload = new { Name = "Updated", Template = "Updated template", Description = "Updated desc" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/systemprompts/{id}", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/systemprompts/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Render_WithoutToken_Returns401()
    {
        // Arrange
        var id = Guid.NewGuid();
        var payload = new { Variables = new Dictionary<string, string> { ["name"] = "World" } };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/systemprompts/{id}/render", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Route constraint validation ─────────────────────────────────────────

    [Fact]
    public async Task GetById_WithInvalidGuid_Returns401()
    {
        // Route constraint removed -> matches route and hits auth -> 401
        var response = await _client.GetAsync("/api/systemprompts/not-a-guid");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_WithInvalidGuid_Returns401()
    {
        // Arrange
        var payload = new { Name = "Updated", Template = "Template", Description = "Desc" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/systemprompts/not-a-guid", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WithInvalidGuid_Returns401()
    {
        // Act
        var response = await _client.DeleteAsync("/api/systemprompts/not-a-guid");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Render_WithInvalidGuid_Returns401()
    {
        // Arrange
        var payload = new { Variables = new Dictionary<string, string>() };

        // Act
        var response = await _client.PostAsJsonAsync("/api/systemprompts/not-a-guid/render", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Route existence: verify endpoints are mapped ────────────────────────

    [Fact]
    public async Task GetAll_WithPagination_WithoutToken_Returns401_RouteExists()
    {
        // Verifies the route accepts query string parameters without 404
        var response = await _client.GetAsync("/api/systemprompts?pageNumber=1&pageSize=10");

        // Should be 401 (not 404) — route exists but requires auth
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithEmptyBody_WithoutToken_Returns401()
    {
        // Auth check comes before model binding/validation
        var response = await _client.PostAsync(
            "/api/systemprompts",
            new StringContent("", Encoding.UTF8, "application/json"));

        // Should be 401 first — auth is checked before request body parsing
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Invalid route variations ────────────────────────────────────────────

    [Fact]
    public async Task NonexistentSubRoute_Returns401OrNotFound()
    {
        // Act — nonexistent sub-route under systemprompts should not expose info
        var response = await _client.GetAsync("/api/systemprompts/nonexistent/unknown");

        // Assert — should return 401 (auth checked before routing) or 404
        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.NotFound)
            .ShouldBeTrue($"Expected 401 or 404 but got {statusCode}");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task GetById_WithNonGuidId_Returns401(string invalidId)
    {
        // Route constraint removed -> matches route and hits auth -> 401
        var response = await _client.GetAsync($"/api/systemprompts/{invalidId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_WithEmptyGuid_WithoutToken_Returns401()
    {
        // Arrange — empty GUID is technically a valid GUID format (all zeros)
        var emptyGuid = Guid.Empty;
        var payload = new { Name = "Test", Template = "Template", Description = "Desc" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/systemprompts/{emptyGuid}", payload);

        // Assert — auth check comes first
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WithEmptyGuid_WithoutToken_Returns401()
    {
        // Arrange — Guid.Empty is a valid GUID format, route matches
        var emptyGuid = Guid.Empty;

        // Act
        var response = await _client.DeleteAsync($"/api/systemprompts/{emptyGuid}");

        // Assert — auth check comes first
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
