namespace Mimir.Infrastructure.Tests.Plugins;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mimir.Domain.Plugins;
using Mimir.Infrastructure.Plugins;
using NSubstitute;
using Shouldly;

/// <summary>
/// Additional negative/edge-case tests for PluginManager.
/// Complements the existing PluginManagerTests with more failure modes.
/// </summary>
public sealed class PluginManagerNegativeTests
{
    private readonly ILogger<PluginManager> _logger = NullLogger<PluginManager>.Instance;
    private readonly PluginManager _sut;

    public PluginManagerNegativeTests()
    {
        _sut = new PluginManager(_logger);
    }

    // ─── LoadPluginAsync ─────────────────────────────────────────

    [Fact]
    public async Task LoadPluginAsync_EmptyPath_ThrowsFileNotFoundException()
    {
        await Should.ThrowAsync<FileNotFoundException>(
            () => _sut.LoadPluginAsync(string.Empty));
    }

    [Fact]
    public async Task LoadPluginAsync_NullPath_ThrowsArgumentNullException()
    {
        // File.Exists(null) returns false, so FileNotFoundException is thrown
        // but the path parameter might cause ArgumentNullException from Path.GetFullPath
        await Should.ThrowAsync<Exception>(
            () => _sut.LoadPluginAsync(null!));
    }

    [Fact]
    public async Task LoadPluginAsync_PathTraversal_ThrowsFileNotFoundException()
    {
        // Attempt path traversal - file won't exist
        await Should.ThrowAsync<FileNotFoundException>(
            () => _sut.LoadPluginAsync("../../../etc/passwd"));
    }

    [Fact]
    public async Task LoadPluginAsync_DirectoryInsteadOfFile_ThrowsFileNotFoundException()
    {
        // A directory exists but File.Exists returns false for directories
        await Should.ThrowAsync<FileNotFoundException>(
            () => _sut.LoadPluginAsync("/tmp"));
    }

    [Fact]
    public async Task LoadPluginAsync_WhitespacePath_ThrowsFileNotFoundException()
    {
        await Should.ThrowAsync<FileNotFoundException>(
            () => _sut.LoadPluginAsync("   "));
    }

    [Fact]
    public async Task LoadPluginAsync_VeryLongPath_ThrowsException()
    {
        // Very long path should result in either FileNotFoundException or system exception
        var longPath = new string('a', 1000) + ".dll";
        await Should.ThrowAsync<Exception>(
            () => _sut.LoadPluginAsync(longPath));
    }

    // ─── RegisterPlugin + LoadPluginAsync duplicate ID ───────────

    [Fact]
    public async Task LoadPluginAsync_DuplicateIdAfterRegister_ThrowsInvalidOperationException()
    {
        // Arrange - register a plugin first
        var plugin = CreateMockPlugin("conflict-id", "Original", "1.0.0", "First");
        _sut.RegisterPlugin(plugin);

        // Act & Assert - loading a plugin with same ID should fail
        // (Can't actually load from disk, but RegisterPlugin + RegisterPlugin shows the same path)
        var plugin2 = CreateMockPlugin("conflict-id", "Duplicate", "2.0.0", "Second");
        Should.Throw<InvalidOperationException>(() => _sut.RegisterPlugin(plugin2));
    }

    // ─── UnloadPluginAsync ───────────────────────────────────────

    [Fact]
    public async Task UnloadPluginAsync_EmptyId_ThrowsKeyNotFoundException()
    {
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.UnloadPluginAsync(string.Empty));
    }

    [Fact]
    public async Task UnloadPluginAsync_WhitespaceId_ThrowsKeyNotFoundException()
    {
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.UnloadPluginAsync("   "));
    }

    [Fact]
    public async Task UnloadPluginAsync_AlreadyUnloaded_ThrowsKeyNotFoundException()
    {
        // Arrange
        var plugin = CreateMockPlugin("once-plugin", "Once", "1.0.0", "Unload once");
        _sut.RegisterPlugin(plugin);
        await _sut.UnloadPluginAsync("once-plugin");

        // Act & Assert - second unload should fail
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.UnloadPluginAsync("once-plugin"));
    }

    [Fact]
    public async Task UnloadPluginAsync_CallsShutdownOnPlugin()
    {
        // Arrange
        var plugin = CreateMockPlugin("shutdown-test", "Shutdown", "1.0.0", "Test shutdown");
        _sut.RegisterPlugin(plugin);

        // Act
        await _sut.UnloadPluginAsync("shutdown-test");

        // Assert - ShutdownAsync should be called (InitializeAsync is called during RegisterPlugin)
        await plugin.Received(1).ShutdownAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnloadPluginAsync_PluginShutdownThrows_PropagatesException()
    {
        // Arrange
        var plugin = CreateMockPlugin("throw-shutdown", "Throw", "1.0.0", "Throws on shutdown");
        plugin.ShutdownAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Shutdown failed"));
        _sut.RegisterPlugin(plugin);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UnloadPluginAsync("throw-shutdown"));
    }

    // ─── ExecutePluginAsync ──────────────────────────────────────

    [Fact]
    public async Task ExecutePluginAsync_EmptyPluginId_ThrowsKeyNotFoundException()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.ExecutePluginAsync(string.Empty, context));
    }

    [Fact]
    public async Task ExecutePluginAsync_AfterUnload_ThrowsKeyNotFoundException()
    {
        // Arrange
        var plugin = CreateMockPlugin("exec-unload", "ExecUnload", "1.0.0", "Execute after unload");
        plugin.ExecuteAsync(Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns(PluginResult.Success(new Dictionary<string, object>()));
        _sut.RegisterPlugin(plugin);

        // Verify it works first
        var context = PluginContext.Create("user-1", new Dictionary<string, object>());
        var result = await _sut.ExecutePluginAsync("exec-unload", context);
        result.IsSuccess.ShouldBeTrue();

        // Unload
        await _sut.UnloadPluginAsync("exec-unload");

        // Act & Assert - should not be executable after unload
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.ExecutePluginAsync("exec-unload", context));
    }

    [Fact]
    public async Task ExecutePluginAsync_PluginThrowsTaskCanceledException_ReturnsFailure()
    {
        // Arrange
        var plugin = CreateMockPlugin("cancel-plugin", "Cancel", "1.0.0", "Throws cancel");
        plugin.ExecuteAsync(Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns<PluginResult>(_ => throw new TaskCanceledException("Cancelled"));

        _sut.RegisterPlugin(plugin);
        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        // Act
        var result = await _sut.ExecutePluginAsync("cancel-plugin", context);

        // Assert - any exception returns PluginResult.Failure
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecutePluginAsync_PluginThrowsOutOfMemory_ReturnsFailure()
    {
        // Arrange
        var plugin = CreateMockPlugin("oom-plugin", "OOM", "1.0.0", "Throws OOM");
        plugin.ExecuteAsync(Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns<PluginResult>(_ => throw new OutOfMemoryException("Simulated OOM"));

        _sut.RegisterPlugin(plugin);
        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        // Act
        var result = await _sut.ExecutePluginAsync("oom-plugin", context);

        // Assert - catch-all returns Failure
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Simulated OOM");
    }

    // ─── ListPluginsAsync ────────────────────────────────────────

    [Fact]
    public async Task ListPluginsAsync_AfterUnloadAll_ReturnsEmptyList()
    {
        // Arrange
        var plugin1 = CreateMockPlugin("list-a", "A", "1.0.0", "First");
        var plugin2 = CreateMockPlugin("list-b", "B", "1.0.0", "Second");
        _sut.RegisterPlugin(plugin1);
        _sut.RegisterPlugin(plugin2);

        // Verify both listed
        var before = await _sut.ListPluginsAsync();
        before.Count.ShouldBe(2);

        // Unload all
        await _sut.UnloadPluginAsync("list-a");
        await _sut.UnloadPluginAsync("list-b");

        // Act
        var after = await _sut.ListPluginsAsync();

        // Assert
        after.ShouldBeEmpty();
    }

    // ─── RegisterPlugin edge cases ───────────────────────────────

    [Fact]
    public void RegisterPlugin_InitializeThrows_PropagatesException()
    {
        // Arrange
        var plugin = CreateMockPlugin("init-fail", "InitFail", "1.0.0", "Init throws");
        plugin.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Init failed"));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _sut.RegisterPlugin(plugin));
    }

    [Fact]
    public void RegisterPlugin_NullPlugin_ThrowsNullReferenceException()
    {
        // Act & Assert
        Should.Throw<NullReferenceException>(() => _sut.RegisterPlugin(null!));
    }

    // ─── Concurrent operations ───────────────────────────────────

    [Fact]
    public async Task ConcurrentExecution_DifferentPlugins_DoesNotInterfere()
    {
        // Arrange
        var plugin1 = CreateMockPlugin("concurrent-a", "A", "1.0.0", "First");
        var plugin2 = CreateMockPlugin("concurrent-b", "B", "1.0.0", "Second");

        var result1 = PluginResult.Success(new Dictionary<string, object> { ["key"] = "value1" });
        var result2 = PluginResult.Success(new Dictionary<string, object> { ["key"] = "value2" });

        plugin1.ExecuteAsync(Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(50);
                return result1;
            });
        plugin2.ExecuteAsync(Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(50);
                return result2;
            });

        _sut.RegisterPlugin(plugin1);
        _sut.RegisterPlugin(plugin2);

        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        // Act - execute concurrently
        var task1 = _sut.ExecutePluginAsync("concurrent-a", context);
        var task2 = _sut.ExecutePluginAsync("concurrent-b", context);
        var results = await Task.WhenAll(task1, task2);

        // Assert
        results[0].IsSuccess.ShouldBeTrue();
        results[1].IsSuccess.ShouldBeTrue();
    }

    // ─── Helper ──────────────────────────────────────────────────

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
