namespace nem.Mimir.Tui.Models;

/// <summary>
/// Represents an available LLM model.
/// </summary>
internal sealed record ModelInfoDto(
    string Id,
    string Name,
    int ContextWindow,
    string RecommendedUse,
    bool IsAvailable);
