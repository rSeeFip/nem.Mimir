using nem.Contracts.Channels;
using nem.Contracts.Content;

namespace nem.Mimir.Application.ChannelEvents;

/// <summary>
/// Abstraction for sending outbound messages to a specific channel.
/// Implementations delegate to the underlying <see cref="IChannelMessageSender"/> from nem.Contracts.
/// </summary>
public interface IChannelMessageSender
{
    /// <summary>The channel type this sender targets.</summary>
    ChannelType Channel { get; }

    /// <summary>Sends a message to the specified channel and target.</summary>
    Task<SendChannelMessageResult> SendAsync(string externalChannelId, IContentPayload content, CancellationToken ct);
}
