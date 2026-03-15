namespace nem.Mimir.Sync.Messages;

/// <summary>
/// Published when a user submits a chat request to the system.
/// </summary>
/// <param name="ConversationId">The conversation this request belongs to.</param>
/// <param name="UserId">The user who initiated the request.</param>
/// <param name="Model">The LLM model requested.</param>
/// <param name="Content">The user's message content.</param>
/// <param name="Timestamp">When the request was received.</param>
public sealed record ChatRequestReceived(
    Guid ConversationId,
    Guid UserId,
    string Model,
    string Content,
    DateTimeOffset Timestamp);
