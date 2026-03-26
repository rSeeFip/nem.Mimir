namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.ValueObjects;
using nem.Contracts.Content;

public class Message : BaseEntity<Guid>
{
    private readonly List<MessageReaction> _reactions = [];

    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? Model { get; private set; }
    public int? TokenCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ParentMessageId { get; private set; }
    public int BranchIndex { get; private set; }
    public bool IsRegenerated { get; private set; }
    public IReadOnlyCollection<MessageReaction> Reactions => _reactions.AsReadOnly();
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
            CreatedAt = DateTimeOffset.UtcNow,
            BranchIndex = 0,
            IsRegenerated = false,
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

    public void Edit(string content)
    {
        if (Role != MessageRole.User)
            throw new InvalidOperationException("Only user messages can be edited.");

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        Content = content.Trim();
    }

    public void AddReaction(string emoji, Guid userId)
    {
        var normalizedEmoji = emoji?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedEmoji))
            throw new ArgumentException("Emoji is required.", nameof(emoji));

        if (normalizedEmoji.Length > 32)
            throw new ArgumentException("Emoji must not exceed 32 characters.", nameof(emoji));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        var existing = _reactions.FirstOrDefault(reaction =>
            reaction.UserId == userId &&
            string.Equals(reaction.Emoji, normalizedEmoji, StringComparison.Ordinal));

        if (existing is not null)
        {
            _reactions.Remove(existing);
            return;
        }

        _reactions.Add(MessageReaction.Create(normalizedEmoji, userId));
    }

    public void RemoveReaction(string emoji, Guid userId)
    {
        var normalizedEmoji = emoji?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedEmoji))
            return;

        var existing = _reactions.FirstOrDefault(reaction =>
            reaction.UserId == userId &&
            string.Equals(reaction.Emoji, normalizedEmoji, StringComparison.Ordinal));

        if (existing is not null)
        {
            _reactions.Remove(existing);
        }
    }

    public void SetBranch(Guid? parentId, int branchIndex)
    {
        if (parentId == Guid.Empty)
            throw new ArgumentException("Parent message ID cannot be empty when provided.", nameof(parentId));

        if (branchIndex < 0)
            throw new ArgumentException("Branch index cannot be negative.", nameof(branchIndex));

        ParentMessageId = parentId;
        BranchIndex = branchIndex;
        IsRegenerated = branchIndex > 0;
    }
}
