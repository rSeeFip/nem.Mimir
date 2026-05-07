namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;

public class Message : BaseEntity<Guid>
{
    public Guid ConversationId { get; private set; }
    public string TenantId { get; private set; } = "default";
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? Model { get; private set; }
    public int? TokenCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Message() { }

    internal static Message Create(Guid conversationId, string tenantId, MessageRole role, string content, string? model = null)
    {
        if (conversationId == Guid.Empty)
            throw new ArgumentException("Conversation ID cannot be empty.", nameof(conversationId));

        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            TenantId = tenantId,
            Role = role,
            Content = content,
            Model = model,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return message;
    }

    public void SetTokenCount(int count)
    {
        if (count < 0)
            throw new ArgumentException("Token count cannot be negative.", nameof(count));

        TokenCount = count;
    }
}
