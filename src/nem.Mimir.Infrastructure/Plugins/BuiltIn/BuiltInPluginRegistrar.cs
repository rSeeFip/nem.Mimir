using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using nem.Mimir.Domain.Plugins;
using nem.Mimir.Finance.McpTools;

namespace nem.Mimir.Infrastructure.Plugins.BuiltIn;

/// <summary>
/// Hosted service that registers built-in plugins with the <see cref="PluginManager"/> at startup.
/// </summary>
internal sealed class BuiltInPluginRegistrar : IHostedService
{
    private readonly PluginManager _pluginManager;
    private readonly IReadOnlyList<IPlugin> _builtInPlugins;
    private readonly ILogger<BuiltInPluginRegistrar> _logger;

    public BuiltInPluginRegistrar(
        PluginManager pluginManager,
        IEnumerable<IPlugin> builtInPlugins,
        ILogger<BuiltInPluginRegistrar> logger)
    {
        _pluginManager = pluginManager;
        _builtInPlugins = builtInPlugins.ToArray();
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var builtInPlugin in _builtInPlugins)
        {
            try
            {
                await _pluginManager.RegisterPluginAsync(builtInPlugin, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Built-in plugin registration failed for {PluginId}", builtInPlugin.Id);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var builtInPlugin in _builtInPlugins.Reverse())
        {
            try
            {
                await _pluginManager.UnloadPluginAsync(builtInPlugin.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogDebug("Built-in plugin {PluginId} was not registered; skipping unload", builtInPlugin.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Built-in plugin unload failed for {PluginId}", builtInPlugin.Id);
            }
        }
    }
}
