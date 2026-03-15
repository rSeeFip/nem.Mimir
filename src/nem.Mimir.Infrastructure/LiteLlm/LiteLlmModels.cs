namespace nem.Mimir.Infrastructure.LiteLlm;

using System.Text.Json;
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

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolDefinitionRequest>? Tools { get; init; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ToolChoice { get; init; }
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

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolCallRequest>? ToolCalls { get; init; }
}

internal sealed record ToolCallRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public required FunctionCallRequest Function { get; init; }
}

internal sealed record FunctionCallRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; }
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

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolCallResponse>? ToolCalls { get; init; }
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

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolCallDelta>? ToolCalls { get; init; }
}

// ─── Models List Response ────────────────────────────────────────

/// <summary>
/// Response from /v1/models endpoint listing available models.
/// </summary>
internal sealed class ModelsListResponse
{
    [JsonPropertyName("data")]
    public IReadOnlyList<ModelInfo>? Data { get; init; }
}

internal sealed class ModelInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("object")]
    public string? Object { get; init; }

    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; init; }
}

// ─── Tool Calling Models ────────────────────────────────────────

internal sealed record ToolDefinitionRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public required FunctionDefinitionRequest Function { get; init; }
}

internal sealed record FunctionDefinitionRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Parameters { get; init; }
}

internal sealed record ToolCallResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public required FunctionCallResponse Function { get; init; }
}

internal sealed record FunctionCallResponse
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; }
}

internal sealed record ToolCallDelta
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }

    [JsonPropertyName("function")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FunctionCallDelta? Function { get; init; }
}

internal sealed record FunctionCallDelta
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Arguments { get; init; }
}
