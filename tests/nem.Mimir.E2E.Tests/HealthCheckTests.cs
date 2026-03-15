using System.Net;
using Shouldly;

namespace nem.Mimir.E2E.Tests;

/// <summary>
/// E2E tests for the health check endpoint (GET /health).
/// Verifies that the health check connects to real PostgreSQL and WireMock LiteLLM.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class HealthCheckTests
{
    private readonly E2EWebApplicationFactory _factory;

    public HealthCheckTests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act — health endpoint is AllowAnonymous, no token needed
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBe("Healthy");
    }
}
