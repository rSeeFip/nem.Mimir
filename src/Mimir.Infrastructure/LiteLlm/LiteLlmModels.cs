namespace Mimir.Infrastructure.LiteLlm;

using System.Text.Json.Serialization;

// ─── Request Models ──────────────────────────────────────────────

/// <summary>
/// OpenAI-compatible chat completion request sent to LiteLLM proxy.
/// </summary>
internal sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<ChatMessageRequest> Messages { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; init; }
}

/// <summary>
/// A single message in the chat completion request.
/// </summary>
internal sealed class ChatMessageRequest
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

// ─── Non-streaming Response Models ───────────────────────────────

/// <summary>
/// OpenAI-compatible chat completion response from LiteLLM proxy.
/// </summary>
internal sealed class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("choices")]
    public IReadOnlyList<ChatCompletionChoice>? Choices { get; init; }

    [JsonPropertyName("usage")]
    public ChatCompletionUsage? Usage { get; init; }
}

internal sealed class ChatCompletionChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("message")]
    public ChatCompletionMessage? Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

internal sealed class ChatCompletionMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

internal sealed class ChatCompletionUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}

// ─── Streaming Response Models ───────────────────────────────────

/// <summary>
/// OpenAI-compatible streaming chat completion chunk from LiteLLM proxy.
/// </summary>
internal sealed class ChatCompletionChunk
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("choices")]
    public IReadOnlyList<ChatCompletionChunkChoice>? Choices { get; init; }
}

internal sealed class ChatCompletionChunkChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("delta")]
    public ChatCompletionDelta? Delta { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

internal sealed class ChatCompletionDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
