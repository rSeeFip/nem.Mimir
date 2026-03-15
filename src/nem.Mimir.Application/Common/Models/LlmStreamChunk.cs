namespace nem.Mimir.Application.Common.Models;

/// <summary>
/// Represents a single chunk of a streaming LLM response.
/// Provider-agnostic representation of a streamed chat completion chunk.
/// </summary>
/// <param name="Content">The text content of this chunk (may be empty for the first/last chunk).</param>
/// <param name="Model">The model that generated the chunk.</param>
/// <param name="FinishReason">The finish reason, if this is the final chunk; otherwise, null.</param>
public sealed record LlmStreamChunk(
    string Content,
    string? Model,
    string? FinishReason);
