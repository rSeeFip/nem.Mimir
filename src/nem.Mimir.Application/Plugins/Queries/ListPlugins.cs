using MediatR;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Plugins;

namespace nem.Mimir.Application.Plugins.Queries;

/// <summary>
/// Query to retrieve the list of all currently loaded plugins and their metadata.
/// </summary>
public sealed record ListPluginsQuery() : IQuery<IReadOnlyList<PluginMetadata>>;

internal sealed class ListPluginsQueryHandler : IRequestHandler<ListPluginsQuery, IReadOnlyList<PluginMetadata>>
{
    private readonly IPluginService _pluginService;

    public ListPluginsQueryHandler(IPluginService pluginService)
    {
        _pluginService = pluginService;
    }

    public async Task<IReadOnlyList<PluginMetadata>> Handle(ListPluginsQuery request, CancellationToken cancellationToken)
    {
        return await _pluginService.ListPluginsAsync(cancellationToken);
    }
}
