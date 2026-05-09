using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nem.Mimir.Domain.Plugins;
using nem.Mimir.Infrastructure.Plugins.BuiltIn;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Plugins.BuiltIn;

public sealed class SkillMarketplacePluginTests
{
    [Fact]
    public async Task InitializeAsync_WithoutConfiguredBaseAddress_DoesNotThrow()
    {
        var plugin = CreatePlugin(new MemoryCache(new MemoryCacheOptions()), CreateClientFactory());

        await plugin.InitializeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutConfiguredBaseAddress_ReturnsFailure()
    {
        var plugin = CreatePlugin(new MemoryCache(new MemoryCacheOptions()), CreateClientFactory());
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["skillId"] = "demo",
            ["parameters"] = "{}",
        });

        var result = await plugin.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("base URL is not configured");
    }

    private static SkillMarketplacePlugin CreatePlugin(IMemoryCache cache, IHttpClientFactory clientFactory)
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IHttpClientFactory)).Returns(clientFactory);
        serviceProvider.GetService(typeof(IMemoryCache)).Returns(cache);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return new SkillMarketplacePlugin(scopeFactory, Substitute.For<ILogger<SkillMarketplacePlugin>>());
    }

    private static IHttpClientFactory CreateClientFactory(Uri? baseAddress = null)
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(SkillMarketplacePlugin.SkillsClientName).Returns(new HttpClient
        {
            BaseAddress = baseAddress,
        });

        return clientFactory;
    }
}
