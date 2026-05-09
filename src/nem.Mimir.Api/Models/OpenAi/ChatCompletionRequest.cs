using System.Text.Json.Serialization;

namespace nem.Mimir.Api.Models.OpenAi;

/// <summary>
/// OpenAI-compatible chat completion request.
/// </summary>
public sealed record ChatCompletionRequest
{
    /// <summary>Model identifier (e.g., "qwen-2.5-72b").</summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>The conversation messages to send.</summary>
    [JsonPropertyName("messages")]
    public required List<ChatMessage> Messages { get; init; }

    /// <summary>Whether to stream the response via SSE.</summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    /// <summary>Sampling temperature (0.0–2.0).</summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    /// <summary>Maximum number of tokens to generate.</summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    /// <summary>Nucleus sampling parameter.</summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; init; }

    /// <summary>Frequency penalty (-2.0 to 2.0).</summary>
    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; init; }

    /// <summary>Presence penalty (-2.0 to 2.0).</summary>
    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; init; }

    /// <summary>Stop sequences that halt generation.</summary>
    [JsonPropertyName("stop")]
    public List<string>? Stop { get; init; }
}

/// <summary>
/// A single message in the chat conversation.
/// </summary>
public sealed record ChatMessage
{
    /// <summary>The role of the message author (system, user, assistant).</summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>The text content of the message.</summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}
