using nem.Mimir.Domain.Plugins;
using nem.Lume.McpTools;
using NSubstitute;
using Shouldly;

namespace nem.Lume.McpTools.Tests;

public sealed class LumeToolRegistryPluginTests
{
    [Fact]
    public async Task LumeMcpTool_ExecuteAsync_ReturnsSuccessWithLumeServerAndTools()
    {
        var plugin = new LumeToolRegistryPlugin();
        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        var result = await plugin.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Data.ContainsKey("server").ShouldBeTrue();
        result.Data["server"].ShouldBe("lume");
        result.Data.ContainsKey("tools").ShouldBeTrue();
    }

    [Fact]
    public async Task LumeMcpTool_ExecuteAsync_ReturnsAllRegistryToolsWithSchemas()
    {
        var plugin = new LumeToolRegistryPlugin();
        var context = PluginContext.Create("user-2", new Dictionary<string, object>());

        var result = await plugin.ExecuteAsync(context, CancellationToken.None);

        var tools = result.Data["tools"].ShouldBeAssignableTo<IReadOnlyList<Dictionary<string, object>>>()!;
        tools.Count.ShouldBe(LumeMcpToolRegistry.GetTools().Count);

        foreach (var tool in tools)
        {
            tool.ContainsKey("name").ShouldBeTrue();
            tool.ContainsKey("description").ShouldBeTrue();
            tool.ContainsKey("action").ShouldBeTrue();
            tool.ContainsKey("inputSchema").ShouldBeTrue();
            tool.ContainsKey("outputSchema").ShouldBeTrue();
        }
    }

    [Fact]
    public async Task LifecycleMethods_DoNotThrow()
    {
        var plugin = new LumeToolRegistryPlugin();

        await plugin.InitializeAsync(CancellationToken.None);
        await plugin.ShutdownAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LumeMcpTool_ExecuteAsync_Cancelled_ThrowsOperationCanceled()
    {
        var plugin = new LumeToolRegistryPlugin();
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
