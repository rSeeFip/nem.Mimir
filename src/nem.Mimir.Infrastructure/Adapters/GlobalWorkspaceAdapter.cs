namespace nem.Mimir.Infrastructure.Adapters;

using MediatR;
using Microsoft.Extensions.Options;
using nem.Contracts.Cognitive;
using nem.MCP.Core.Cognitive;

public sealed class GlobalWorkspaceAdapter
{
    public static Task Handle(
        WorkspaceBroadcastEvent message,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return mediator.Publish(new WorkspaceBroadcastNotification(message), cancellationToken);
    }
}

public sealed class WorkspaceBroadcastNotificationHandler : INotificationHandler<WorkspaceBroadcastNotification>
{
    private readonly IReadOnlyList<ICognitiveAgent> _agents;
    private readonly GlobalWorkspaceAdapterOptions _options;

    public WorkspaceBroadcastNotificationHandler(
        IEnumerable<ICognitiveAgent> agents,
        IOptions<GlobalWorkspaceAdapterOptions> options)
    {
        _agents = agents.ToList();
        _options = options.Value;
    }

    public async Task Handle(WorkspaceBroadcastNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        foreach (var agent in _agents)
        {
            if (!agent.IsActive || !_options.IsParticipating(agent.ServiceName))
            {
                continue;
            }

            foreach (var entry in notification.BroadcastEvent.Entries)
            {
                await agent.OnWorkspaceEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
