namespace Mimir.Domain.Plugins;

/// <summary>
/// Contract that all plugins must implement to be loaded by the PluginManager.
/// </summary>
public interface IPlugin
{
    /// <summary>Unique identifier for this plugin.</summary>
    string Id { get; }

    /// <summary>Human-readable plugin name.</summary>
    string Name { get; }

    /// <summary>Semantic version string.</summary>
    string Version { get; }

    /// <summary>Short description of what the plugin does.</summary>
    string Description { get; }

    /// <summary>Executes the plugin with the given context.</summary>
    Task<PluginResult> ExecuteAsync(PluginContext context, CancellationToken ct = default);

    /// <summary>Called once when the plugin is loaded to perform initialization.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Called when the plugin is being unloaded to release resources.</summary>
    Task ShutdownAsync(CancellationToken ct = default);
}
