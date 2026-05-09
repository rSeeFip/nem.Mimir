using Wolverine;
using nem.Contracts.Content;
using nem.Contracts.Events.Integration;

namespace nem.Mimir.Application.ChannelEvents;

public static class ChannelEventReceivedConsumer
{
    public static async Task Handle(global::nem.Contracts.Events.Integration.ChannelEventReceivedIntegrationEvent @event, IMessageBus bus, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var command = new IngestChannelEventCommand(
            @event.ChannelType,
            @event.ExternalChannelId,
            @event.SenderId,
            new TextContent(@event.Content) { CreatedAt = @event.Timestamp },
            @event.Timestamp);

        await bus.InvokeAsync<ChannelEventResult>(command, ct);
    }
}
