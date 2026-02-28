namespace Mimir.Sync.Messages;

/// <summary>
/// Published when a message is persisted in a conversation.
/// </summary>
/// <param name="ConversationId">The conversation the message belongs to.</param>
/// <param name="MessageId">The unique identifier of the message.</param>
/// <param name="UserId">The user who sent or triggered the message.</param>
/// <param name="Role">The role of the message sender (e.g. "user", "assistant").</param>
/// <param name="Timestamp">When the message was sent.</param>
public sealed record MessageSent(
    Guid ConversationId,
    Guid MessageId,
    Guid UserId,
    string Role,
    DateTimeOffset Timestamp);
