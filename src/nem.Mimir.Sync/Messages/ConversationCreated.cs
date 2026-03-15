namespace nem.Mimir.Sync.Messages;

/// <summary>
/// Published when a new conversation is created.
/// </summary>
/// <param name="ConversationId">The unique identifier of the new conversation.</param>
/// <param name="UserId">The user who created the conversation.</param>
/// <param name="Title">The initial title of the conversation.</param>
/// <param name="Timestamp">When the conversation was created.</param>
public sealed record ConversationCreated(
    Guid ConversationId,
    Guid UserId,
    string Title,
    DateTimeOffset Timestamp);
