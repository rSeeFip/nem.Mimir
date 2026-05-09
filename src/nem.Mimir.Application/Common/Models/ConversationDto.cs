namespace nem.Mimir.Application.Common.Models;

/// <summary>
/// Data transfer object representing a conversation with its messages.
/// </summary>
/// <param name="Id">The conversation's unique identifier.</param>
/// <param name="UserId">The identifier of the user who owns the conversation.</param>
/// <param name="Title">The conversation title.</param>
/// <param name="Status">The conversation status name.</param>
/// <param name="Messages">The messages in this conversation.</param>
/// <param name="CreatedAt">The timestamp when the conversation was created.</param>
/// <param name="UpdatedAt">The timestamp when the conversation was last updated, if any.</param>
public sealed record ConversationDto(
    Guid Id,
    Guid UserId,
    Guid? FolderId,
    bool IsPinned,
    string? ShareId,
    IReadOnlyList<string> Tags,
    string Title,
    string Status,
    IReadOnlyList<MessageDto> Messages,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
