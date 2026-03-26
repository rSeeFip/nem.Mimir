namespace nem.Mimir.Application.Common.Models;

/// <summary>
/// Data transfer object representing a message within a conversation.
/// </summary>
/// <param name="Id">The message's unique identifier.</param>
/// <param name="ConversationId">The identifier of the conversation this message belongs to.</param>
/// <param name="Role">The message role (User, Assistant, System).</param>
/// <param name="Content">The message content text.</param>
/// <param name="Model">The LLM model used to generate this message, if applicable.</param>
/// <param name="TokenCount">The token count for this message, if calculated.</param>
/// <param name="CreatedAt">The timestamp when the message was created.</param>
public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    string Role,
    string Content,
    string? Model,
    int? TokenCount,
    DateTimeOffset CreatedAt,
    Guid? ParentMessageId,
    int BranchIndex,
    bool IsRegenerated,
    IReadOnlyList<ConversationMessageReactionDto> Reactions);

public sealed record ConversationMessageReactionDto(
    string Emoji,
    Guid UserId,
    DateTimeOffset ReactedAt);
