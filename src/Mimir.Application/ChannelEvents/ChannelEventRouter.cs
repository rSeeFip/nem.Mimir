using nem.Contracts.Channels;

namespace Mimir.Application.ChannelEvents;

/// <summary>
/// Routes outbound messages to the correct <see cref="IChannelMessageSender"/> based on channel type.
/// </summary>
public sealed class ChannelEventRouter
{
    private readonly Dictionary<ChannelType, IChannelMessageSender> _senders;

    public ChannelEventRouter(IEnumerable<IChannelMessageSender> senders)
    {
        _senders = senders.ToDictionary(s => s.Channel);
    }

    /// <summary>
    /// Gets the sender registered for the specified channel type.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no sender is registered for the channel.</exception>
    public IChannelMessageSender GetSender(ChannelType channel)
        => _senders.TryGetValue(channel, out var sender)
            ? sender
            : throw new InvalidOperationException($"No sender registered for channel '{channel}'.");
}
