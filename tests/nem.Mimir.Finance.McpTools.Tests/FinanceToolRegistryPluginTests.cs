using nem.Mimir.Domain.Plugins;
using nem.Mimir.Finance.McpTools;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Finance.McpTools.Tests;

public sealed class FinanceToolRegistryPluginTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessWithFinanceServerAndTools()
    {
        var plugin = new FinanceToolRegistryPlugin();
        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        var result = await plugin.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Data.ContainsKey("server").ShouldBeTrue();
        result.Data["server"].ShouldBe("finance");
        result.Data.ContainsKey("tools").ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsAllFiveToolsWithSchemas()
    {
        var plugin = new FinanceToolRegistryPlugin();
        var context = PluginContext.Create("user-2", new Dictionary<string, object>());

        var result = await plugin.ExecuteAsync(context, CancellationToken.None);

        var tools = result.Data["tools"].ShouldBeAssignableTo<IReadOnlyList<Dictionary<string, object>>>()!;
        tools.Count.ShouldBe(5);

        foreach (var tool in tools)
        {
            tool.ContainsKey("name").ShouldBeTrue();
            tool.ContainsKey("description").ShouldBeTrue();
            tool.ContainsKey("inputSchema").ShouldBeTrue();
            tool.ContainsKey("outputSchema").ShouldBeTrue();
        }
    }

    [Fact]
    public async Task LifecycleMethods_DoNotThrow()
    {
        var plugin = new FinanceToolRegistryPlugin();

        await plugin.InitializeAsync(CancellationToken.None);
        await plugin.ShutdownAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_ThrowsOperationCanceled()
    {
        var plugin = new FinanceToolRegistryPlugin();
        var context = PluginContext.Create("user-3", new Dictionary<string, object>());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => plugin.ExecuteAsync(context, cts.Token));
    }

    [Fact]
    public void CanUseNSubstitute_MockSanity()
    {
        var dependency = Substitute.For<IDisposable>();

        dependency.Dispose();

        dependency.Received(1).Dispose();
    }
}
