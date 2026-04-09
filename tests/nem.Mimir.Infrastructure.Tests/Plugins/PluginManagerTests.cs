using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using nem.Mimir.Domain.Plugins;
using nem.Mimir.Infrastructure.Plugins;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Plugins;

public sealed class PluginManagerTests
{
    private readonly ILogger<PluginManager> _logger = NullLogger<PluginManager>.Instance;
    private readonly PluginManager _sut;

    public PluginManagerTests()
    {
        _sut = new PluginManager(_logger);
    }

    [Fact]
    public async Task ListPluginsAsync_Initially_ShouldReturnEmptyList()
    {
        var result = await _sut.ListPluginsAsync();

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadPluginAsync_NonExistentPath_ShouldThrowFileNotFoundException()
    {
        await Should.ThrowAsync<FileNotFoundException>(
            () => _sut.LoadPluginAsync("/nonexistent/path.dll"));
    }

    [Fact]
    public async Task RegisterPlugin_ShouldMakePluginListable()
    {
        // Arrange
        var plugin = CreateMockPlugin("test-plugin", "Test Plugin", "1.0.0", "A test");

        // Act
        await _sut.RegisterPluginAsync(plugin);
        var list = await _sut.ListPluginsAsync();

        // Assert
        list.Count.ShouldBe(1);
        list[0].Id.ShouldBe("test-plugin");
        list[0].Name.ShouldBe("Test Plugin");
        list[0].Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task RegisterPlugin_DuplicateId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var plugin1 = CreateMockPlugin("dup-plugin", "Plugin 1", "1.0.0", "First");
        var plugin2 = CreateMockPlugin("dup-plugin", "Plugin 2", "2.0.0", "Second");

        // Act
        await _sut.RegisterPluginAsync(plugin1);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _sut.RegisterPluginAsync(plugin2));
    }

    [Fact]
    public async Task ExecutePluginAsync_LoadedPlugin_ShouldReturnResult()
    {
        // Arrange
        var expectedResult = PluginResult.Success(
            new Dictionary<string, object> { ["output"] = "hello" });

        var plugin = CreateMockPlugin("exec-plugin", "Exec Plugin", "1.0.0", "Execute test");
        plugin.ExecuteAsync(Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        await _sut.RegisterPluginAsync(plugin);

        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        // Act
        var result = await _sut.ExecutePluginAsync("exec-plugin", context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data["output"].ShouldBe("hello");
        await plugin.Received(1).ExecuteAsync(context, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecutePluginAsync_UnknownPlugin_ShouldThrowKeyNotFoundException()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.ExecutePluginAsync("unknown-plugin", context));
    }

    [Fact]
    public async Task ExecutePluginAsync_PluginThrows_ShouldReturnFailureResult()
    {
        // Arrange
        var plugin = CreateMockPlugin("bad-plugin", "Bad Plugin", "1.0.0", "Throws");
        plugin.ExecuteAsync(Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns<PluginResult>(_ => throw new InvalidOperationException("Plugin crashed"));

        await _sut.RegisterPluginAsync(plugin);
        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        // Act
        var result = await _sut.ExecutePluginAsync("bad-plugin", context);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Plugin crashed");
    }

    [Fact]
    public async Task UnloadPluginAsync_LoadedPlugin_ShouldRemoveFromList()
    {
        // Arrange
        var plugin = CreateMockPlugin("unload-plugin", "Unload Plugin", "1.0.0", "Unload test");
        await _sut.RegisterPluginAsync(plugin);

        // Verify it's listed
        var listBefore = await _sut.ListPluginsAsync();
        listBefore.Count.ShouldBe(1);

        // Act
        await _sut.UnloadPluginAsync("unload-plugin");

        // Assert
        var listAfter = await _sut.ListPluginsAsync();
        listAfter.ShouldBeEmpty();
        await plugin.Received(1).ShutdownAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnloadPluginAsync_UnknownPlugin_ShouldThrowKeyNotFoundException()
    {
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.UnloadPluginAsync("not-loaded"));
    }

    [Fact]
    public async Task RegisterPlugin_ShouldCallInitializeAsync()
    {
        // Arrange
        var plugin = CreateMockPlugin("init-plugin", "Init Plugin", "1.0.0", "Init test");

        // Act
        await _sut.RegisterPluginAsync(plugin);

        // Assert
        await plugin.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MultiplePlugins_ShouldAllBeListable()
    {
        // Arrange
        var plugin1 = CreateMockPlugin("plugin-a", "A", "1.0.0", "First");
        var plugin2 = CreateMockPlugin("plugin-b", "B", "2.0.0", "Second");
        var plugin3 = CreateMockPlugin("plugin-c", "C", "3.0.0", "Third");

        // Act
        await _sut.RegisterPluginAsync(plugin1);
        await _sut.RegisterPluginAsync(plugin2);
        await _sut.RegisterPluginAsync(plugin3);

        var list = await _sut.ListPluginsAsync();

        // Assert
        list.Count.ShouldBe(3);
        list.ShouldContain(m => m.Id == "plugin-a");
        list.ShouldContain(m => m.Id == "plugin-b");
        list.ShouldContain(m => m.Id == "plugin-c");
    }

    private static IPlugin CreateMockPlugin(string id, string name, string version, string description)
    {
        var plugin = Substitute.For<IPlugin>();
        plugin.Id.Returns(id);
        plugin.Name.Returns(name);
        plugin.Version.Returns(version);
        plugin.Description.Returns(description);
        plugin.InitializeAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        plugin.ShutdownAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return plugin;
    }
}
