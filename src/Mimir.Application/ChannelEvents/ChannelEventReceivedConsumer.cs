using MediatR;
using nem.Contracts.Content;
using nem.Contracts.Events.Integration;

namespace Mimir.Application.ChannelEvents;

public static class ChannelEventReceivedConsumer
{
    public static async Task Handle(global::nem.Contracts.Events.Integration.ChannelEventReceivedIntegrationEvent @event, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var command = new IngestChannelEventCommand(
            @event.ChannelType,
            @event.ExternalChannelId,
            @event.SenderId,
            new TextContent(@event.Content) { CreatedAt = @event.Timestamp },
            @event.Timestamp);

        await sender.Send(command, ct);
    }
}
