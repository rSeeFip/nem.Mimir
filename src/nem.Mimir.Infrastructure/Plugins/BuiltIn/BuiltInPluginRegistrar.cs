using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using nem.Mimir.Domain.Plugins;
using nem.Mimir.Finance.McpTools;

namespace nem.Mimir.Infrastructure.Plugins.BuiltIn;

internal sealed class BuiltInPluginRegistrar : IHostedService
{
    private readonly IPluginRuntimeCatalog _pluginRuntimeCatalog;
    private readonly IReadOnlyList<IBuiltInPlugin> _builtInPlugins;
    private readonly ILogger<BuiltInPluginRegistrar> _logger;

    public BuiltInPluginRegistrar(
        IPluginRuntimeCatalog pluginRuntimeCatalog,
        IEnumerable<IBuiltInPlugin> builtInPlugins,
        ILogger<BuiltInPluginRegistrar> logger)
    {
        _pluginRuntimeCatalog = pluginRuntimeCatalog;
        _builtInPlugins = builtInPlugins.ToArray();
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var builtInPlugin in _builtInPlugins)
        {
            try
            {
                await _pluginRuntimeCatalog.RegisterPluginAsync(builtInPlugin, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Built-in plugin registration failed for {PluginId}", builtInPlugin.Id);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _pluginRuntimeCatalog.UnloadAllAsync(cancellationToken);
    }
}
