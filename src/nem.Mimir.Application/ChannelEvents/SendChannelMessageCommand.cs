using nem.Contracts.Channels;
using nem.Contracts.Content;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.ChannelEvents;

/// <summary>
/// Command to send an outbound message to a channel adapter.
/// </summary>
/// <param name="Channel">The target channel to send the message to.</param>
/// <param name="ExternalChannelId">The channel-specific identifier for the target conversation/chat.</param>
/// <param name="Content">The content payload to deliver.</param>
public sealed record SendChannelMessageCommand(
    ChannelType Channel,
    string ExternalChannelId,
    IContentPayload Content) : ICommand<SendChannelMessageResult>;

/// <summary>
/// Result of sending a channel message.
/// </summary>
/// <param name="Success">Whether the message was delivered successfully.</param>
/// <param name="MessageRef">The channel-specific message reference, if available.</param>
/// <param name="ErrorMessage">Error description when delivery fails.</param>
public sealed record SendChannelMessageResult(
    bool Success,
    ChannelMessageRef? MessageRef,
    string? ErrorMessage);
