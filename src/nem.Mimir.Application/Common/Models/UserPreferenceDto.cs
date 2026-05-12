namespace nem.Mimir.Application.Common.Models;

public sealed record UserPreferenceDto(
    Guid Id,
    Guid UserId,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> Settings,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
