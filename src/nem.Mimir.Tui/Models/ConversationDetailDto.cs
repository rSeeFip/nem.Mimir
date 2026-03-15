namespace nem.Mimir.Tui.Models;

/// <summary>
/// Represents a conversation with messages returned by the REST API.
/// </summary>
internal sealed record ConversationDetailDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Status,
    IReadOnlyList<MessageItemDto> Messages,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Represents a message within a conversation.
/// </summary>
internal sealed record MessageItemDto(
    Guid Id,
    Guid ConversationId,
    string Role,
    string Content,
    string? Model,
    int? TokenCount,
    DateTimeOffset CreatedAt);
