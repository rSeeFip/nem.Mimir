using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests for the <see cref="Controllers.AdminController"/>.
/// Verifies endpoint routing and authentication requirements.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AdminControllerTests
{
    private readonly HttpClient _client;

    public AdminControllerTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("GET", "/api/admin/users")]
    [InlineData("GET", "/api/admin/users/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/admin/users/00000000-0000-0000-0000-000000000001/role")]
    [InlineData("POST", "/api/admin/users/00000000-0000-0000-0000-000000000001/deactivate")]
    [InlineData("GET", "/api/admin/audit")]
    public async Task AdminEndpoint_WithoutToken_Returns401(string method, string endpoint)
    {
        // Arrange
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);

        if (method == "PUT")
        {
            request.Content = JsonContent.Create(new { Role = "Admin" });
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
            $"Unauthenticated {method} {endpoint} should return 401 Unauthorized");
    }

    [Theory]
    [InlineData("/api/admin/users")]
    [InlineData("/api/admin/audit")]
    public async Task AdminGetEndpoint_WithoutToken_DoesNotReturn200(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        response.StatusCode.ShouldNotBe(HttpStatusCode.OK,
            $"GET {endpoint} should not be accessible without authentication");
    }

    [Fact]
    public async Task GetAllUsers_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_WithoutToken_Returns401()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/admin/users/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUserRole_WithoutToken_Returns401()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var content = JsonContent.Create(new { Role = "Admin" });

        // Act
        var response = await _client.PutAsync($"/api/admin/users/{userId}/role", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivateUser_WithoutToken_Returns401()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/admin/users/{userId}/deactivate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuditLog_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/audit");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuditLog_WithQueryParams_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/admin/audit?userId=00000000-0000-0000-0000-000000000001&action=UserCreated&pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_InvalidRoute_Returns401OrNotFound()
    {
        // Act — nonexistent sub-route under admin should not expose info
        var response = await _client.GetAsync("/api/admin/nonexistent");

        // Assert — should return 401 (auth checked before routing) or 404
        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.NotFound)
            .ShouldBeTrue($"Expected 401 or 404 but got {statusCode}");
    }
}
