namespace nem.Mimir.Signal.Tests.Health;

using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Signal.Configuration;
using nem.Mimir.Signal.Health;
using nem.Mimir.Signal.Services;
using NSubstitute;
using Shouldly;

public sealed class SignalHealthCheckTests
{
    private readonly SignalSettings _settings = new()
    {
        ApiBaseUrl = "http://localhost:8080",
        PhoneNumber = "+1234567890",
    };

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenApiReachable()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var healthCheck = CreateHealthCheck(handler);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenApiUnreachable()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var healthCheck = CreateHealthCheck(handler);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_OnException()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
        var healthCheck = CreateHealthCheck(handler);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    private SignalHealthCheck CreateHealthCheck(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(_settings.ApiBaseUrl) };
        var options = Substitute.For<IOptions<SignalSettings>>();
        options.Value.Returns(_settings);
        var apiClientLogger = Substitute.For<ILogger<SignalApiClient>>();
        var apiClient = new SignalApiClient(httpClient, options, apiClientLogger);
        var healthCheckLogger = Substitute.For<ILogger<SignalHealthCheck>>();
        return new SignalHealthCheck(apiClient, healthCheckLogger);
    }
}
