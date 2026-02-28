using System.Net;
using System.Net.Http.Json;
using System.Text;
using Shouldly;

namespace Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests for the <see cref="Controllers.ConversationsController"/>.
/// Focuses on authentication enforcement and correct routing/status codes.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ConversationsControllerTests
{
    private readonly HttpClient _client;

    public ConversationsControllerTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("GET", "/api/conversations")]
    [InlineData("POST", "/api/conversations")]
    [InlineData("GET", "/api/conversations/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/conversations/00000000-0000-0000-0000-000000000001/title")]
    [InlineData("POST", "/api/conversations/00000000-0000-0000-0000-000000000001/archive")]
    [InlineData("DELETE", "/api/conversations/00000000-0000-0000-0000-000000000001")]
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
    public async Task GetAll_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/conversations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/conversations/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        // Arrange
        var payload = new { Title = "Test", SystemPrompt = (string?)null, Model = (string?)null };

        // Act
        var response = await _client.PostAsJsonAsync("/api/conversations", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateTitle_WithoutToken_Returns401()
    {
        // Arrange
        var id = Guid.NewGuid();
        var payload = new { Title = "Updated" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/conversations/{id}/title", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Archive_WithoutToken_Returns401()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/conversations/{id}/archive", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/conversations/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_WithInvalidGuid_Returns404()
    {
        // Route constraint {id:guid} means invalid GUIDs don't match any route → 404
        var response = await _client.GetAsync("/api/conversations/not-a-guid");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_WithPaginationParams_Returns401_ButRouteExists()
    {
        // Verifies the route accepts query string parameters without 404
        var response = await _client.GetAsync("/api/conversations?pageNumber=1&pageSize=10");

        // Should be 401 (not 404) — route exists but requires auth
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
