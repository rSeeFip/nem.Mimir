namespace nem.Mimir.Infrastructure.Adapters;

using nem.Contracts.Cognitive;
using Wolverine;

public sealed record MimirWorkspaceResponseNotification(WorkspaceEntry Entry);

public static class MimirWorkspaceResponseHandler
{
    public static async Task Handle(MimirWorkspaceResponseNotification notification, IMessageBus messageBus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        await messageBus.PublishAsync(notification.Entry).ConfigureAwait(false);
    }
}
