namespace Mimir.Infrastructure.Tests.ChannelEvents;

using MediatR;
using Mimir.Application.ChannelEvents;
using NSubstitute;
using Shouldly;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using nem.Contracts.Events.Integration;

public sealed class ChannelEventReceivedConsumerTests
{
    [Fact]
    public async Task Handle_MapsIntegrationEventToIngestChannelEventCommand_AndSendsViaMediatR()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IngestChannelEventCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelEventResult(Guid.NewGuid(), true));

        var inbound = new ChannelEventReceivedIntegrationEvent(
            ChannelType.Teams,
            "chat-77",
            "user-9",
            "Ada",
            "hello from teams",
            new DateTimeOffset(2026, 03, 10, 10, 0, 0, TimeSpan.Zero),
            "{\"raw\":true}");

        await ChannelEventReceivedConsumer.Handle(inbound, sender, CancellationToken.None);

        await sender.Received(1).Send(
            Arg.Is<IngestChannelEventCommand>(x =>
                x.Channel == ChannelType.Teams
                && x.ExternalChannelId == "chat-77"
                && x.ExternalUserId == "user-9"
                && x.Timestamp == inbound.Timestamp
                && x.Content.Equals(new TextContent("hello from teams") { CreatedAt = inbound.Timestamp })),
            Arg.Any<CancellationToken>());
    }
}
