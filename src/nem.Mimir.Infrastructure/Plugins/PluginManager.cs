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

        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        PluginLoadContext? loadContext = null;
        IPlugin? plugin = null;

        try
        {
            loadContext = new PluginLoadContext(fullAssemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(fullAssemblyPath);

            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                ?? throw new InvalidOperationException(
                    $"No type implementing IPlugin found in assembly: {fullAssemblyPath}");

            plugin = CreatePluginInstance(pluginType);
            await plugin.InitializeAsync(ct).ConfigureAwait(false);

            var metadata = PluginMetadata.Create(plugin.Id, plugin.Name, plugin.Version, plugin.Description);
            var entry = new PluginEntry(plugin, metadata, loadContext);

            if (!_plugins.TryAdd(plugin.Id, entry))
            {
                await SafeShutdownAsync(plugin, ct).ConfigureAwait(false);
                SafeUnload(loadContext);
                throw new InvalidOperationException($"A plugin with ID '{plugin.Id}' is already loaded.");
            }

            _logger.LogInformation("Plugin loaded: {PluginId} v{Version}", plugin.Id, plugin.Version);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin from {AssemblyPath}", fullAssemblyPath);

            if (plugin is not null)
            {
                await SafeShutdownAsync(plugin, ct).ConfigureAwait(false);
            }

            if (loadContext is not null)
            {
                SafeUnload(loadContext);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        if (!_plugins.TryRemove(pluginId, out var entry))
        {
            throw new KeyNotFoundException($"Plugin '{pluginId}' is not loaded.");
        }

        await SafeShutdownAsync(entry.Plugin, ct).ConfigureAwait(false);
        SafeUnload(entry.LoadContext);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin '{PluginId}' execution failed", pluginId);
            return PluginResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Registers a pre-instantiated plugin (for testing or in-process plugins).
    /// </summary>
    internal async Task RegisterPluginAsync(IPlugin plugin, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        var metadata = PluginMetadata.Create(plugin.Id, plugin.Name, plugin.Version, plugin.Description);

        try
        {
            await plugin.InitializeAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Built-in plugin registration failed during initialization for {PluginId}", plugin.Id);
            return;
        }

        var entry = new PluginEntry(plugin, metadata, null);

        if (!_plugins.TryAdd(plugin.Id, entry))
        {
            await SafeShutdownAsync(plugin, ct).ConfigureAwait(false);
            throw new InvalidOperationException($"A plugin with ID '{plugin.Id}' is already loaded.");
        }

        _logger.LogInformation("Plugin registered: {PluginId} v{Version}", plugin.Id, plugin.Version);
    }

    private static IPlugin CreatePluginInstance(Type pluginType)
    {
        return (IPlugin)(Activator.CreateInstance(pluginType)
            ?? throw new InvalidOperationException(
                $"Failed to create instance of plugin type: {pluginType.FullName}"));
    }

    private async Task SafeShutdownAsync(IPlugin plugin, CancellationToken ct)
    {
        try
        {
            await plugin.ShutdownAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin '{PluginId}' shutdown failed", plugin.Id);
        }
    }

    private void SafeUnload(PluginLoadContext? loadContext)
    {
        if (loadContext is null)
        {
            return;
        }

        try
        {
            loadContext.Unload();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin load context unload failed");
        }
    }

    private sealed record PluginEntry(IPlugin Plugin, PluginMetadata Metadata, PluginLoadContext? LoadContext);
}
