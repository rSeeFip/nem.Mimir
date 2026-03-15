namespace nem.Mimir.Teams.Tests.Services;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using nem.Mimir.Teams.Configuration;
using nem.Mimir.Teams.Health;
using Shouldly;

public sealed class TeamsHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_EmptyAppId_ReturnsUnhealthy()
    {
        var settings = Options.Create(new TeamsSettings { AppId = string.Empty });
        var sut = new TeamsHealthCheck(settings, NullLogger<TeamsHealthCheck>.Instance);

        var result = await sut.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_WhitespaceAppId_ReturnsUnhealthy()
    {
        var settings = Options.Create(new TeamsSettings { AppId = "   " });
        var sut = new TeamsHealthCheck(settings, NullLogger<TeamsHealthCheck>.Instance);

        var result = await sut.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ValidAppId_ReturnsHealthy()
    {
        var settings = Options.Create(new TeamsSettings
        {
            AppId = "valid-app-id",
            AppPassword = "valid-password",
        });
        var sut = new TeamsHealthCheck(settings, NullLogger<TeamsHealthCheck>.Instance);

        var result = await sut.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        // With valid config (but no real Bot Framework connection), we expect Healthy
        // since we only check configuration presence
        result.Status.ShouldBe(HealthStatus.Healthy);
    }
}
