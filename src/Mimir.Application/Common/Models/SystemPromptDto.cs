namespace Mimir.Application.Common.Models;

/// <summary>
/// Data transfer object representing a system prompt.
/// </summary>
/// <param name="Id">The system prompt's unique identifier.</param>
/// <param name="Name">The system prompt name.</param>
/// <param name="Template">The template text with variable placeholders.</param>
/// <param name="Description">The system prompt description.</param>
/// <param name="IsDefault">Whether this is the default system prompt.</param>
/// <param name="IsActive">Whether this system prompt is active.</param>
/// <param name="CreatedAt">The timestamp when the system prompt was created.</param>
/// <param name="UpdatedAt">The timestamp when the system prompt was last updated, if any.</param>
public sealed record SystemPromptDto(
    Guid Id,
    string Name,
    string Template,
    string Description,
    bool IsDefault,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
