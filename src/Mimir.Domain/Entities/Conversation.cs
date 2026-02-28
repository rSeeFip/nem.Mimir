namespace Mimir.Domain.Entities;

using Mimir.Domain.Common;
using Mimir.Domain.Enums;
using Mimir.Domain.Events;

public class Conversation : BaseAuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public ConversationStatus Status { get; private set; }

    private readonly List<Message> _messages = [];
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

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
            CreatedAt = DateTimeOffset.UtcNow
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

        UpdatedAt = DateTimeOffset.UtcNow;

        return message;
    }

    public void Archive()
    {
        Status = ConversationStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        Title = title;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
