namespace nem.Mimir.Application.Common.Models;

/// <summary>
/// Data transfer object for conversation list items (without messages).
/// </summary>
/// <param name="Id">The conversation's unique identifier.</param>
/// <param name="UserId">The identifier of the user who owns the conversation.</param>
/// <param name="Title">The conversation title.</param>
/// <param name="Status">The conversation status name.</param>
/// <param name="MessageCount">The number of messages in the conversation.</param>
/// <param name="CreatedAt">The timestamp when the conversation was created.</param>
/// <param name="UpdatedAt">The timestamp when the conversation was last updated, if any.</param>
public sealed record ConversationListDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Status,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
