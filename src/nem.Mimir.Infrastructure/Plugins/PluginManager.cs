using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Plugins;

namespace nem.Mimir.Infrastructure.Plugins;

/// <summary>
/// Manages plugin lifecycle — loading from assemblies, unloading, listing, and executing.
/// Each plugin runs in its own <see cref="PluginLoadContext"/> for isolation.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
internal sealed class PluginManager : IPluginService
{
    private readonly ConcurrentDictionary<string, PluginEntry> _plugins = new();
    private readonly ILogger<PluginManager> _logger;

    public PluginManager(ILogger<PluginManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PluginMetadata> LoadPluginAsync(string assemblyPath, CancellationToken ct = default)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Plugin assembly not found: {assemblyPath}", assemblyPath);
        }

        var loadContext = new PluginLoadContext(assemblyPath);
        var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));

        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            ?? throw new InvalidOperationException(
                $"No type implementing IPlugin found in assembly: {assemblyPath}");

        var plugin = (IPlugin)(Activator.CreateInstance(pluginType)
            ?? throw new InvalidOperationException(
                $"Failed to create instance of plugin type: {pluginType.FullName}"));

        await plugin.InitializeAsync(ct);

        var metadata = PluginMetadata.Create(plugin.Id, plugin.Name, plugin.Version, plugin.Description);
        var entry = new PluginEntry(plugin, metadata, loadContext);

        if (!_plugins.TryAdd(plugin.Id, entry))
        {
            await plugin.ShutdownAsync(ct);
            loadContext.Unload();
            throw new InvalidOperationException($"A plugin with ID '{plugin.Id}' is already loaded.");
        }

        _logger.LogInformation("Plugin loaded: {PluginId} v{Version}", plugin.Id, plugin.Version);
        return metadata;
    }

    /// <inheritdoc />
    public async Task UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        if (!_plugins.TryRemove(pluginId, out var entry))
        {
            throw new KeyNotFoundException($"Plugin '{pluginId}' is not loaded.");
        }

        await entry.Plugin.ShutdownAsync(ct);
        entry.LoadContext?.Unload();

        _logger.LogInformation("Plugin unloaded: {PluginId}", pluginId);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PluginMetadata>> ListPluginsAsync(CancellationToken ct = default)
    {
        var list = _plugins.Values
            .Select(e => e.Metadata)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<PluginMetadata>>(list);
    }

    /// <inheritdoc />
    public async Task<PluginResult> ExecutePluginAsync(string pluginId, PluginContext context, CancellationToken ct = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var entry))
        {
            throw new KeyNotFoundException($"Plugin '{pluginId}' is not loaded.");
        }

        try
        {
            return await entry.Plugin.ExecuteAsync(context, ct);
        }
        catch (Exception ex) // Intentional catch-all: plugins are third-party and can throw any exception type
        {
            _logger.LogError(ex, "Plugin '{PluginId}' execution failed", pluginId);
            return PluginResult.Failure(ex.Message);
        }
    }

    internal void RegisterPlugin(IPlugin plugin)
    {
        var metadata = PluginMetadata.Create(plugin.Id, plugin.Name, plugin.Version, plugin.Description);
        var entry = new PluginEntry(plugin, metadata, null);

        if (!_plugins.TryAdd(plugin.Id, entry))
        {
            throw new InvalidOperationException($"A plugin with ID '{plugin.Id}' is already loaded.");
        }

        // Intentional sync-over-async: internal registration helper used from BuiltInPluginRegistrar and test setup.
        // Cannot propagate async without modifying test files (which is out of scope).
        plugin.InitializeAsync().GetAwaiter().GetResult();
        _logger.LogInformation("Plugin registered: {PluginId} v{Version}", plugin.Id, plugin.Version);
    }

    private sealed record PluginEntry(IPlugin Plugin, PluginMetadata Metadata, PluginLoadContext? LoadContext);
}
