namespace Mimir.Domain.Entities;

using Mimir.Domain.Common;
using nem.Contracts.Channels;
using nem.Contracts.Content;

public sealed class ChannelEvent : BaseEntity<Guid>
{
    public ChannelType Channel { get; private set; }
    public string ExternalChannelId { get; private set; } = string.Empty;
    public string ExternalUserId { get; private set; } = string.Empty;
    public IContentPayload? ContentPayload { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    private ChannelEvent() { }

    public static ChannelEvent Create(
        ChannelType channel,
        string externalChannelId,
        string externalUserId,
        IContentPayload? contentPayload,
        DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(externalChannelId))
            throw new ArgumentException("External channel ID cannot be empty.", nameof(externalChannelId));

        if (string.IsNullOrWhiteSpace(externalUserId))
            throw new ArgumentException("External user ID cannot be empty.", nameof(externalUserId));

        return new ChannelEvent
        {
            Id = Guid.NewGuid(),
            Channel = channel,
            ExternalChannelId = externalChannelId,
            ExternalUserId = externalUserId,
            ContentPayload = contentPayload,
            ReceivedAt = receivedAt
        };
    }
}
