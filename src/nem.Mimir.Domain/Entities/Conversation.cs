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
}
