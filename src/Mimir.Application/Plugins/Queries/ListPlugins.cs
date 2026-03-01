using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Plugins;

namespace Mimir.Application.Plugins.Queries;

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
