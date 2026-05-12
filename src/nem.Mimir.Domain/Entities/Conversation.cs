namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Events;
using nem.Mimir.Domain.ValueObjects;

public class Conversation : BaseAuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public ConversationStatus Status { get; private set; }
    public ConversationSettings? Settings { get; private set; }
    public Guid? FolderId { get; private set; }
    public bool IsPinned { get; private set; }
    public string? ShareId { get; private set; }
    public Guid? ParentConversationId { get; private set; }
    public string? ForkReason { get; private set; }

    private readonly List<Message> _messages = [];
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private readonly List<string> _tags = [];
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();

    private Conversation() { }

    public static Conversation Create(Guid userId, string title)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Status = ConversationStatus.Active,
        };

        conversation.AddDomainEvent(new ConversationCreatedEvent(conversation.Id, userId));

        return conversation;
    }

    public Message AddMessage(MessageRole role, string content, string? model = null)
    {
        if (Status == ConversationStatus.Archived)
            throw new InvalidOperationException("Cannot add messages to archived conversation.");

        var message = Message.Create(Id, role, content, model);
        _messages.Add(message);

        AddDomainEvent(new Events.MessageSentEvent(message.Id, Id, role));


        return message;
    }

    public void Archive()
    {
        Status = ConversationStatus.Archived;
    }

    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        Title = title;
    }

    public void UpdateSettings(ConversationSettings settings)
    {
        Settings = settings;
    }

    public void Pin()
    {
        IsPinned = true;
    }

    public void Unpin()
    {
        IsPinned = false;
    }

    public void Share()
    {
        if (!string.IsNullOrWhiteSpace(ShareId))
        {
            return;
        }

        ShareId = Guid.NewGuid().ToString("N")[..12];
    }

    public void Unshare()
    {
        ShareId = null;
    }

    public void AddTag(string tag)
    {
        var normalizedTag = ConversationTag.Create(tag).Value;
        if (_tags.Any(existing => string.Equals(existing, normalizedTag, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _tags.Add(normalizedTag);
    }

    public void RemoveTag(string tag)
    {
        var normalizedTag = ConversationTag.Create(tag).Value;
        var existingTag = _tags.FirstOrDefault(existing => string.Equals(existing, normalizedTag, StringComparison.OrdinalIgnoreCase));
        if (existingTag is null)
        {
            return;
        }

        _tags.Remove(existingTag);
    }

    public void MoveToFolder(Guid? folderId)
    {
        FolderId = folderId;
    }

    public Conversation Fork(Guid userId, string forkReason)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(forkReason))
            throw new ArgumentException("Fork reason cannot be empty.", nameof(forkReason));

        var forked = new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = $"{Title} (fork)",
            Status = ConversationStatus.Active,
            ParentConversationId = Id,
            ForkReason = forkReason,
        };

        forked.AddDomainEvent(new ConversationForkedEvent(forked.Id, Id, forkReason, userId));

        return forked;
    }
}
