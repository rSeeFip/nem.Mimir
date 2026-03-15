using System.Net;
using Shouldly;

namespace nem.Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests for the admin restore entity endpoint.
/// Verifies endpoint routing and authentication requirements.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AdminRestoreEntityTests
{
    private readonly HttpClient _client;

    public AdminRestoreEntityTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("conversation")]
    [InlineData("user")]
    [InlineData("systemprompt")]
    public async Task RestoreEntity_WithoutToken_Returns401(string entityType)
    {
        // Arrange
        var entityId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync(
            $"/api/admin/restore/{entityType}/{entityId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
            $"POST /api/admin/restore/{entityType}/{entityId} should return 401 Unauthorized");
    }

    [Fact]
    public async Task RestoreEntity_WithInvalidGuid_Returns401OrNotFound()
    {
        // Act — invalid GUID format should not match the route constraint
        var response = await _client.PostAsync(
            "/api/admin/restore/conversation/not-a-guid", null);

        // Assert — should return 401 (auth checked before routing) or 404 (route mismatch)
        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.NotFound)
            .ShouldBeTrue($"Expected 401 or 404 but got {statusCode}");
    }
}
