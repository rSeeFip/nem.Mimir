using Mimir.Application.Common.Models;

namespace Mimir.Application.Common.Interfaces;

/// <summary>
/// Service interface for interacting with Large Language Model providers.
/// Abstracts away the specific LLM SDK to maintain clean architecture boundaries.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Sends a message to the LLM and returns the complete response.
    /// </summary>
    /// <param name="model">The model identifier (e.g., "gpt-4", "claude-3").</param>
    /// <param name="messages">The conversation message history to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete LLM response.</returns>
    Task<LlmResponse> SendMessageAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to the LLM and streams the response as chunks.
    /// </summary>
    /// <param name="model">The model identifier (e.g., "gpt-4", "claude-3").</param>
    /// <param name="messages">The conversation message history to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of response chunks.</returns>
    IAsyncEnumerable<LlmStreamChunk> StreamMessageAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken = default);
}
