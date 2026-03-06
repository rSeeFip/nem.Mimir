namespace Mimir.WhatsApp.Tests.Services;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mimir.WhatsApp.Configuration;
using Mimir.WhatsApp.Health;
using Shouldly;

public sealed class WhatsAppHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_AllConfigured_ReturnsHealthy()
    {
        var settings = new WhatsAppSettings
        {
            AccessToken = "token",
            PhoneNumberId = "12345",
            VerifyToken = "verify",
        };
        var healthCheck = new WhatsAppHealthCheck(
            Options.Create(settings),
            NullLogger<WhatsAppHealthCheck>.Instance);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_NoAccessToken_ReturnsUnhealthy()
    {
        var settings = new WhatsAppSettings { AccessToken = "" };
        var healthCheck = new WhatsAppHealthCheck(
            Options.Create(settings),
            NullLogger<WhatsAppHealthCheck>.Instance);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("access token");
    }

    [Fact]
    public async Task CheckHealthAsync_NoPhoneNumberId_ReturnsDegraded()
    {
        var settings = new WhatsAppSettings
        {
            AccessToken = "token",
            PhoneNumberId = "",
            VerifyToken = "verify",
        };
        var healthCheck = new WhatsAppHealthCheck(
            Options.Create(settings),
            NullLogger<WhatsAppHealthCheck>.Instance);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("phone number");
    }

    [Fact]
    public async Task CheckHealthAsync_NoVerifyToken_ReturnsDegraded()
    {
        var settings = new WhatsAppSettings
        {
            AccessToken = "token",
            PhoneNumberId = "12345",
            VerifyToken = "",
        };
        var healthCheck = new WhatsAppHealthCheck(
            Options.Create(settings),
            NullLogger<WhatsAppHealthCheck>.Instance);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description!.ShouldContain("verify token");
    }
}
