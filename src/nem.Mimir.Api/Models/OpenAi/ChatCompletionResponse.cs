using System.Text.Json.Serialization;

namespace nem.Mimir.Api.Models.OpenAi;

/// <summary>
/// OpenAI-compatible streaming chat completion chunk (SSE payload).
/// </summary>
public sealed record ChatCompletionChunk
{
    /// <summary>Unique identifier for this completion (e.g., "chatcmpl-abc123").</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Object type, always "chat.completion.chunk" for streaming.</summary>
    [JsonPropertyName("object")]
    public string Object { get; init; } = "chat.completion.chunk";

    /// <summary>Unix timestamp (seconds) when the chunk was created.</summary>
    [JsonPropertyName("created")]
    public long Created { get; init; }

    /// <summary>The model that generated the completion.</summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>The list of completion choices.</summary>
    [JsonPropertyName("choices")]
    public required List<ChunkChoice> Choices { get; init; }
}

/// <summary>
/// A single choice within a streaming chat completion chunk.
/// </summary>
public sealed record ChunkChoice
{
    /// <summary>The index of this choice in the choices array.</summary>
    [JsonPropertyName("index")]
    public int Index { get; init; }

    /// <summary>The delta content for this chunk.</summary>
    [JsonPropertyName("delta")]
    public required ChunkDelta Delta { get; init; }

    /// <summary>The reason the model stopped generating (null while streaming, "stop" at end).</summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

/// <summary>
/// Delta content within a streaming chunk.
/// </summary>
public sealed record ChunkDelta
{
    /// <summary>The role of the author (only present in the first chunk).</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>The content fragment for this chunk.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

/// <summary>
/// OpenAI-compatible non-streaming chat completion response.
/// </summary>
public sealed record ChatCompletionResult
{
    /// <summary>Unique identifier for this completion.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Object type, always "chat.completion" for non-streaming.</summary>
    [JsonPropertyName("object")]
    public string Object { get; init; } = "chat.completion";

    /// <summary>Unix timestamp (seconds) when the completion was created.</summary>
    [JsonPropertyName("created")]
    public long Created { get; init; }

    /// <summary>The model that generated the completion.</summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>The list of completion choices.</summary>
    [JsonPropertyName("choices")]
    public required List<ResultChoice> Choices { get; init; }

    /// <summary>Token usage statistics.</summary>
    [JsonPropertyName("usage")]
    public required UsageInfo Usage { get; init; }
}

/// <summary>
/// A single choice in a non-streaming chat completion response.
/// </summary>
public sealed record ResultChoice
{
    /// <summary>The index of this choice.</summary>
    [JsonPropertyName("index")]
    public int Index { get; init; }

    /// <summary>The generated message.</summary>
    [JsonPropertyName("message")]
    public required ResultMessage Message { get; init; }

    /// <summary>The reason generation stopped.</summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

/// <summary>
/// A message in a non-streaming chat completion response.
/// </summary>
public sealed record ResultMessage
{
    /// <summary>The role of the author.</summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = "assistant";

    /// <summary>The content of the message.</summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

/// <summary>
/// Token usage statistics for a chat completion.
/// </summary>
public sealed record UsageInfo
{
    /// <summary>Number of tokens in the prompt.</summary>
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    /// <summary>Number of tokens in the completion.</summary>
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    /// <summary>Total number of tokens used.</summary>
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
