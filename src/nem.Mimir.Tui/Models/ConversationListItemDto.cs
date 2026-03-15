namespace nem.Mimir.Tui.Models;

/// <summary>
/// Represents a conversation summary returned by the REST API.
/// </summary>
internal sealed record ConversationListItemDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Status,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
