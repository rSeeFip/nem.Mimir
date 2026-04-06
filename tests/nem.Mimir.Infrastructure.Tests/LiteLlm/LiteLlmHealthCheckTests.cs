namespace nem.Mimir.Infrastructure.Tests.LiteLlm;

using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using nem.Mimir.Infrastructure.LiteLlm;
using Shouldly;

public sealed class LiteLlmHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenModelsEndpointSucceeds()
    {
        var client = new HttpClient(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://litellm.test"),
        };
        var factory = new StubHttpClientFactory(client);
        var healthCheck = new LiteLlmHealthCheck(factory, NullLogger<LiteLlmHealthCheck>.Instance);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenModelsEndpointFails()
    {
        var client = new HttpClient(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("http://litellm.test"),
        };
        var factory = new StubHttpClientFactory(client);
        var healthCheck = new LiteLlmHealthCheck(factory, NullLogger<LiteLlmHealthCheck>.Instance);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull();
        result.Description.ShouldContain("401");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenRequestThrows()
    {
        var client = new HttpClient(new ThrowingHttpMessageHandler())
        {
            BaseAddress = new Uri("http://litellm.test"),
        };
        var factory = new StubHttpClientFactory(client);
        var healthCheck = new LiteLlmHealthCheck(factory, NullLogger<LiteLlmHealthCheck>.Instance);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("LiteLLM health check failed.");
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri!.PathAndQuery.ShouldBe("/v1/models");
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection refused");
        }
    }
}
