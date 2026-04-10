using nem.Mimir.Domain.Plugins;

namespace nem.Mimir.Infrastructure.Plugins;

internal interface IPluginRuntimeCatalog
{
    Task RegisterPluginAsync(IPlugin plugin, CancellationToken ct = default);

    Task UnloadAllAsync(CancellationToken ct = default);
}
