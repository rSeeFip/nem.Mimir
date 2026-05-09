namespace nem.Mimir.Application.Common.Models;

public sealed record UserMemoryDto(
    Guid Id,
    Guid UserId,
    string Content,
    string? Context,
    string Source,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
