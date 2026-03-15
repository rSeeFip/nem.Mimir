using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Repository interface for SystemPrompt entity persistence operations.
/// </summary>
public interface ISystemPromptRepository
{
    /// <summary>
    /// Gets a system prompt by its unique identifier.
    /// </summary>
    Task<SystemPrompt?> GetByIdAsync(SystemPromptId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all system prompts with pagination.
    /// </summary>
    Task<PaginatedList<SystemPrompt>> GetAllAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new system prompt.
    /// </summary>
    Task<SystemPrompt> CreateAsync(SystemPrompt systemPrompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing system prompt.
    /// </summary>
    Task UpdateAsync(SystemPrompt systemPrompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a system prompt by its identifier.
    /// </summary>
    Task DeleteAsync(SystemPromptId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default system prompt, if one is set.
    /// </summary>
    Task<SystemPrompt?> GetDefaultAsync(CancellationToken cancellationToken = default);
}
