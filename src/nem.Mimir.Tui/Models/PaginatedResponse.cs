namespace nem.Mimir.Tui.Models;

/// <summary>
/// Represents a paginated API response.
/// </summary>
internal sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int TotalPages,
    int TotalCount);
