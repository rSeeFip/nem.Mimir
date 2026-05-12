namespace nem.Mimir.Application.Common.Models;

/// <summary>
/// Represents the complete response from an LLM provider.
/// Provider-agnostic representation of a chat completion response.
/// </summary>
/// <param name="Content">The generated text content.</param>
/// <param name="Model">The model that generated the response.</param>
/// <param name="PromptTokens">The number of tokens in the prompt.</param>
/// <param name="CompletionTokens">The number of tokens in the completion.</param>
/// <param name="TotalTokens">The total number of tokens used.</param>
/// <param name="FinishReason">The reason the model stopped generating (e.g., "stop", "length").</param>
public sealed record LlmResponse(
    string Content,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    string? FinishReason);
