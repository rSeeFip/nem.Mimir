using System.Net;
using Shouldly;

namespace Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests verifying health check endpoint behavior.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class HealthCheckTests
{
    private readonly HttpClient _client;

    public HealthCheckTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsOkOrDegraded_WithoutAuthentication()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert — health endpoint should be accessible without authentication
        // It may return 200 (Healthy) or 503 (Unhealthy/Degraded) depending on DB state,
        // but it should NOT return 401 or 404.
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }
}
