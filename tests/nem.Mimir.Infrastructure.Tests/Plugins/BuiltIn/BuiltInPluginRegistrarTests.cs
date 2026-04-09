using Microsoft.Extensions.Logging.Abstractions;
using nem.Mimir.Domain.Plugins;
using nem.Mimir.Finance.McpTools;
using nem.Mimir.Infrastructure.Plugins;
using nem.Mimir.Infrastructure.Plugins.BuiltIn;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Plugins.BuiltIn;

public sealed class BuiltInPluginRegistrarTests
{
    private readonly PluginManager _pluginManager = new(NullLogger<PluginManager>.Instance);
    private readonly NullLogger<BuiltInPluginRegistrar> _logger = NullLogger<BuiltInPluginRegistrar>.Instance;

    [Fact]
    public async Task StartAsync_ShouldRegisterAllBuiltInPluginsViaDependencyInjection()
    {
        var plugin1 = CreatePlugin("built-in-a", "Built In A");
        var plugin2 = CreatePlugin("built-in-b", "Built In B");

        var sut = new BuiltInPluginRegistrar(_pluginManager, [plugin1, plugin2], _logger);

        await sut.StartAsync(CancellationToken.None);

        var plugins = await _pluginManager.ListPluginsAsync();
        plugins.Select(x => x.Id).ShouldBe(["built-in-a", "built-in-b"], ignoreOrder: true);
        await plugin1.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        await plugin2.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenBuiltInPluginInitializationFails_ShouldContinueRegisteringOthers()
    {
        var failingPlugin = CreatePlugin("built-in-fail", "Built In Fail");
        failingPlugin.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("boom"));

        var healthyPlugin = CreatePlugin("built-in-ok", "Built In Ok");

        var sut = new BuiltInPluginRegistrar(_pluginManager, [failingPlugin, healthyPlugin], _logger);

        await sut.StartAsync(CancellationToken.None);

        var plugins = await _pluginManager.ListPluginsAsync();
        plugins.Count.ShouldBe(1);
        plugins[0].Id.ShouldBe("built-in-ok");
    }

    [Fact]
    public async Task StopAsync_ShouldUnloadRegisteredBuiltInPlugins()
    {
        var plugin1 = CreatePlugin("built-in-a", "Built In A");
        var plugin2 = CreatePlugin("built-in-b", "Built In B");

        var sut = new BuiltInPluginRegistrar(_pluginManager, [plugin1, plugin2], _logger);

        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        var plugins = await _pluginManager.ListPluginsAsync();
        plugins.ShouldBeEmpty();
        await plugin1.Received(1).ShutdownAsync(Arg.Any<CancellationToken>());
        await plugin2.Received(1).ShutdownAsync(Arg.Any<CancellationToken>());
    }

    private static IPlugin CreatePlugin(string id, string name)
    {
        var plugin = Substitute.For<IPlugin>();
        plugin.Id.Returns(id);
        plugin.Name.Returns(name);
        plugin.Version.Returns("1.0.0");
        plugin.Description.Returns("Built-in test plugin");
        plugin.InitializeAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        plugin.ShutdownAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        plugin.ExecuteAsync(Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns(PluginResult.Success(new Dictionary<string, object>()));
        return plugin;
    }
}
