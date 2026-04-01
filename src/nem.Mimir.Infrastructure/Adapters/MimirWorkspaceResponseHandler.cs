namespace nem.Mimir.Infrastructure.Adapters;

using MediatR;
using nem.Contracts.Cognitive;
using Wolverine;

public sealed record MimirWorkspaceResponseNotification(WorkspaceEntry Entry) : INotification;

public sealed class MimirWorkspaceResponseHandler : INotificationHandler<MimirWorkspaceResponseNotification>
{
    private readonly IMessageBus _messageBus;

    public MimirWorkspaceResponseHandler(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }

    public async Task Handle(MimirWorkspaceResponseNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        await _messageBus.PublishAsync(notification.Entry).ConfigureAwait(false);
    }
}
