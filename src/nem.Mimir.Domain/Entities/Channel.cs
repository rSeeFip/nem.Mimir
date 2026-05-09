namespace nem.Mimir.Domain.Entities;

using ChannelId = nem.Contracts.Identity.ChannelId;
using ChannelMessageId = nem.Contracts.Identity.ChannelMessageId;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Events;

public sealed class Channel : BaseAuditableEntity<ChannelId>
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public ChannelType Type { get; private set; }
    public Guid? SourceConversationId { get; private set; }

    private readonly List<ChannelMember> _members = [];
    public IReadOnlyCollection<ChannelMember> Members => _members.AsReadOnly();

    private readonly List<ChannelMessage> _messages = [];
    public IReadOnlyCollection<ChannelMessage> Messages => _messages.AsReadOnly();

    private Channel() { }

    public static Channel Create(Guid ownerId, string name, string? description, ChannelType type)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Owner ID cannot be empty.", nameof(ownerId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Channel name cannot be empty.", nameof(name));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > 100)
        {
            throw new ArgumentException("Channel name must not exceed 100 characters.", nameof(name));
        }

        var trimmedDescription = description?.Trim();
        if (!string.IsNullOrEmpty(trimmedDescription) && trimmedDescription.Length > 500)
        {
            throw new ArgumentException("Channel description must not exceed 500 characters.", nameof(description));
        }

        var channel = new Channel
        {
            Id = ChannelId.New(),
            OwnerId = ownerId,
            Name = trimmedName,
            Description = string.IsNullOrEmpty(trimmedDescription) ? null : trimmedDescription,
            Type = type,
        };

        channel.AddMember(ownerId, ChannelMemberRole.Owner);
        channel.AddDomainEvent(new ChannelCreatedEvent(channel.Id, ownerId));

        return channel;
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Channel name cannot be empty.", nameof(name));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > 100)
        {
            throw new ArgumentException("Channel name must not exceed 100 characters.", nameof(name));
        }

        var trimmedDescription = description?.Trim();
        if (!string.IsNullOrEmpty(trimmedDescription) && trimmedDescription.Length > 500)
        {
            throw new ArgumentException("Channel description must not exceed 500 characters.", nameof(description));
        }

        Name = trimmedName;
        Description = string.IsNullOrEmpty(trimmedDescription) ? null : trimmedDescription;
    }

    public ChannelMember AddMember(Guid userId, ChannelMemberRole role = ChannelMemberRole.Member)
    {
        if (_members.Any(member => member.UserId == userId))
        {
            return _members.First(member => member.UserId == userId);
        }

        var member = ChannelMember.Create(Id, userId, role);
        _members.Add(member);
        AddDomainEvent(new MemberJoinedEvent(Id, userId));
        return member;
    }

    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(candidate => candidate.UserId == userId);
        if (member is null)
        {
            return;
        }

        _members.Remove(member);
        AddDomainEvent(new MemberLeftEvent(Id, userId));
    }

    public ChannelMessage AddMessage(Guid senderId, string content, Guid? parentMessageId = null)
    {
        if (!_members.Any(member => member.UserId == senderId))
        {
            throw new InvalidOperationException("Sender is not a channel member.");
        }

        ChannelMessageId? typedParentId = parentMessageId.HasValue ? ChannelMessageId.From(parentMessageId.Value) : null;
        var message = ChannelMessage.Create(Id, senderId, content, typedParentId);
        _messages.Add(message);
        AddDomainEvent(new ChannelMessageSentEvent(message.Id, Id, senderId));
        return message;
    }

    public void SetSourceConversation(Guid conversationId)
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation ID cannot be empty.", nameof(conversationId));
        }

        SourceConversationId = conversationId;
    }
}
