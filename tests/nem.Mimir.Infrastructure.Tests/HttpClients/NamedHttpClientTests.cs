namespace nem.Mimir.Infrastructure.Tests.HttpClients;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using nem.Mimir.Infrastructure;
using nem.Mimir.Infrastructure.LiteLlm;
using Shouldly;

public sealed class NamedHttpClientTests
{
    [Fact]
    public void LiteLlmClient_ResolvesFromFactory()
    {
        var services = new ServiceCollection();
        services.AddHttpClient(LiteLlmClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("http://localhost:4000");
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient(LiteLlmClient.HttpClientName);

        client.ShouldNotBeNull();
        client.BaseAddress.ShouldBe(new Uri("http://localhost:4000"));
    }

    [Fact]
    public void McpClient_ResolvesFromFactory()
    {
        var services = new ServiceCollection();
        services.AddHttpClient(HttpClientNames.Mcp, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient(HttpClientNames.Mcp);

        client.ShouldNotBeNull();
        client.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ExternalClient_ResolvesFromFactory()
    {
        var services = new ServiceCollection();
        services.AddHttpClient(HttpClientNames.External, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient(HttpClientNames.External);

        client.ShouldNotBeNull();
        client.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void AllThreeNamedClients_ResolveAsDistinctInstances()
    {
        var services = new ServiceCollection();
        services.AddHttpClient(LiteLlmClient.HttpClientName);
        services.AddHttpClient(HttpClientNames.Mcp);
        services.AddHttpClient(HttpClientNames.External);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var litellm = factory.CreateClient(LiteLlmClient.HttpClientName);
        var mcp = factory.CreateClient(HttpClientNames.Mcp);
        var external = factory.CreateClient(HttpClientNames.External);

        litellm.ShouldNotBeNull();
        mcp.ShouldNotBeNull();
        external.ShouldNotBeNull();

        ReferenceEquals(litellm, mcp).ShouldBeFalse();
        ReferenceEquals(mcp, external).ShouldBeFalse();
    }

    [Fact]
    public void HttpClientNames_HaveExpectedValues()
    {
        HttpClientNames.Mcp.ShouldBe("mcp");
        HttpClientNames.External.ShouldBe("external");
        LiteLlmClient.HttpClientName.ShouldBe("LiteLlm");
    }
}
