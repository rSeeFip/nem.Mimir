namespace nem.Mimir.Application.OpenAiCompat.Models;

/// <summary>
/// Application-layer DTO representing a single model in the OpenAI-compatible models list.
/// </summary>
/// <param name="Id">The model identifier.</param>
/// <param name="OwnedBy">The organization that owns the model.</param>
public sealed record OpenAiModelDto(string Id, string OwnedBy = "mimir");

/// <summary>
/// Application-layer DTO representing a chat message for OpenAI-compatible endpoints.
/// </summary>
/// <param name="Role">The role of the message author (system, user, assistant).</param>
/// <param name="Content">The text content of the message.</param>
public sealed record ChatMessageDto(string Role, string Content);

/// <summary>
/// Application-layer result of a non-streaming chat completion.
/// </summary>
public sealed record ChatCompletionResultDto
{
    /// <summary>The generated assistant response content.</summary>
    public required string Content { get; init; }

    /// <summary>The model that actually generated the completion.</summary>
    public required string Model { get; init; }

    /// <summary>The reason the model stopped generating.</summary>
    public required string FinishReason { get; init; }

    /// <summary>Number of tokens in the prompt.</summary>
    public int PromptTokens { get; init; }

    /// <summary>Number of tokens in the completion.</summary>
    public int CompletionTokens { get; init; }

    /// <summary>Total number of tokens used.</summary>
    public int TotalTokens { get; init; }

    /// <summary>Duration of the LLM call.</summary>
    public required TimeSpan Duration { get; init; }
}
