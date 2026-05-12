namespace nem.Mimir.Domain.Entities;

using ChannelId = nem.Contracts.Identity.ChannelId;
using ChannelMessageId = nem.Contracts.Identity.ChannelMessageId;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Events;
using nem.Mimir.Domain.ValueObjects;

public sealed class ChannelMessage : BaseAuditableEntity<ChannelMessageId>
{
    public ChannelId ChannelId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public ChannelMessageId? ParentMessageId { get; private set; }
    public bool IsPinned { get; private set; }

    private readonly List<ChannelMessageReaction> _reactions = [];
    public IReadOnlyCollection<ChannelMessageReaction> Reactions => _reactions.AsReadOnly();

    private ChannelMessage() { }

    public static ChannelMessage Create(
        ChannelId channelId,
        Guid senderId,
        string content,
        ChannelMessageId? parentMessageId = null)
    {
        if (channelId.IsEmpty)
            throw new ArgumentException("Channel ID cannot be empty.", nameof(channelId));

        if (senderId == Guid.Empty)
            throw new ArgumentException("Sender ID cannot be empty.", nameof(senderId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        if (content.Length > 4000)
            throw new ArgumentException("Content must not exceed 4000 characters.", nameof(content));

        return new ChannelMessage
        {
            Id = ChannelMessageId.New(),
            ChannelId = channelId,
            SenderId = senderId,
            Content = content.Trim(),
            ParentMessageId = parentMessageId,
            IsPinned = false,
        };
    }

    public void Edit(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        if (content.Length > 4000)
            throw new ArgumentException("Content must not exceed 4000 characters.", nameof(content));

        Content = content.Trim();
    }

    public void Pin() => IsPinned = true;

    public void Unpin() => IsPinned = false;

    public void AddReaction(Guid userId, string emoji)
    {
        if (_reactions.Any(r => r.UserId == userId && string.Equals(r.Emoji, emoji, StringComparison.Ordinal)))
            return;

        var reaction = ChannelMessageReaction.Create(emoji, userId);
        _reactions.Add(reaction);
        AddDomainEvent(new MessageReactedEvent(ChannelId, Id, userId, reaction.Emoji));
    }

    public void RemoveReaction(Guid userId, string emoji)
    {
        var reaction = _reactions.FirstOrDefault(r => r.UserId == userId && string.Equals(r.Emoji, emoji, StringComparison.Ordinal));
        if (reaction is null)
            return;

        _reactions.Remove(reaction);
    }
}
