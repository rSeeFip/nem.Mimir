namespace Mimir.Domain.Entities;

using Mimir.Domain.Common;
using Mimir.Domain.Enums;
using nem.Contracts.Content;

public class Message : BaseEntity<Guid>
{
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? Model { get; private set; }
    public int? TokenCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IContentPayload? ContentPayload { get; private set; }
    public string? ContentType { get; private set; }

    public IContentPayload ResolvedPayload =>
        ContentPayload ?? new TextContent(Content);

    private Message() { }

    internal static Message Create(Guid conversationId, MessageRole role, string content, string? model = null)
    {
        if (conversationId == Guid.Empty)
            throw new ArgumentException("Conversation ID cannot be empty.", nameof(conversationId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            Model = model,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return message;
    }

    public void SetContentPayload(IContentPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ContentPayload = payload;
        ContentType = payload.ContentType;
    }

    public void SetTokenCount(int count)
    {
        if (count < 0)
            throw new ArgumentException("Token count cannot be negative.", nameof(count));

        TokenCount = count;
    }
}
