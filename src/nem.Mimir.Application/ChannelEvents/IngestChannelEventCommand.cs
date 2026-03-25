using nem.Contracts.Channels;
using nem.Contracts.Content;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.ChannelEvents;

/// <summary>
/// Command to ingest an inbound message received from a channel adapter.
/// </summary>
/// <param name="Channel">The channel this event originated from.</param>
/// <param name="ExternalChannelId">The channel-specific identifier for the conversation/chat.</param>
/// <param name="ExternalUserId">The channel-specific user identifier of the sender.</param>
/// <param name="Content">The content payload of the message.</param>
/// <param name="Timestamp">When the message was received from the channel.</param>
public sealed record IngestChannelEventCommand(
    ChannelType Channel,
    string ExternalChannelId,
    string ExternalUserId,
    IContentPayload Content,
    DateTimeOffset Timestamp) : ICommand<ChannelEventResult>;

/// <summary>
/// Result of ingesting a channel event.
/// </summary>
/// <param name="EventId">The unique identifier assigned to the ingested event.</param>
/// <param name="Accepted">Whether the event was accepted for processing.</param>
public sealed record ChannelEventResult(
    Guid EventId,
    bool Accepted);
