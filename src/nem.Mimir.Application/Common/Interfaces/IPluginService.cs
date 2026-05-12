using nem.Mimir.Domain.Plugins;

namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Application service for managing plugins — loading, unloading, listing, and executing.
/// </summary>
public interface IPluginService
{
    /// <summary>Loads a plugin from the specified assembly path.</summary>
    Task<PluginMetadata> LoadPluginAsync(string assemblyPath, CancellationToken ct = default);

    /// <summary>Unloads a previously loaded plugin by its identifier.</summary>
    Task UnloadPluginAsync(string pluginId, CancellationToken ct = default);

    /// <summary>Lists metadata for all currently loaded plugins.</summary>
    Task<IReadOnlyList<PluginMetadata>> ListPluginsAsync(CancellationToken ct = default);

    /// <summary>Executes a loaded plugin with the given context.</summary>
    Task<PluginResult> ExecutePluginAsync(string pluginId, PluginContext context, CancellationToken ct = default);
}
