namespace nem.Mimir.Infrastructure.Adapters;

using Microsoft.Extensions.Options;
using nem.Contracts.Cognitive;
using nem.MCP.Core.Cognitive;
using Wolverine;

public sealed class GlobalWorkspaceAdapter
{
    public static Task Handle(
        WorkspaceBroadcastEvent message,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return messageBus.InvokeAsync(new WorkspaceBroadcastNotification(message), cancellationToken);
    }
}

public sealed class WorkspaceBroadcastNotificationHandler
{
    public async Task Handle(
        WorkspaceBroadcastNotification notification,
        IEnumerable<ICognitiveAgent> agents,
        IOptions<GlobalWorkspaceAdapterOptions> options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(options);

        var participatingAgents = agents.ToList();
        var adapterOptions = options.Value;

        foreach (var agent in participatingAgents)
        {
            if (!agent.IsActive || !adapterOptions.IsParticipating(agent.ServiceName))
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
