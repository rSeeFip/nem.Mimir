using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Manages LLM context window construction, including system prompt resolution,
/// token estimation, and history truncation.
/// </summary>
public interface IContextWindowService
{
    /// <summary>
    /// Builds the ordered list of LLM messages for a conversation, applying context window
    /// truncation to fit within the model's token limit. Resolves the system prompt from
    /// the database, falling back to a built-in default.
    /// </summary>
    Task<IReadOnlyList<LlmMessage>> BuildLlmMessagesAsync(
        Conversation conversation,
        string newUserContent,
        string? model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the maximum token limit for the given model identifier.
    /// </summary>
    int GetTokenLimit(string? model);

    /// <summary>
    /// Estimates the token count of a text string using a character-based heuristic.
    /// </summary>
    int EstimateTokenCount(string text);
}
