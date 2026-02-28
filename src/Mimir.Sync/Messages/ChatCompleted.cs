namespace Mimir.Sync.Messages;

/// <summary>
/// Published when a chat completion finishes, carrying token usage and latency metrics.
/// </summary>
/// <param name="ConversationId">The conversation the completion belongs to.</param>
/// <param name="MessageId">The identifier of the generated assistant message.</param>
/// <param name="Model">The LLM model that produced the response.</param>
/// <param name="PromptTokens">Number of prompt tokens consumed.</param>
/// <param name="CompletionTokens">Number of completion tokens generated.</param>
/// <param name="Duration">Wall-clock duration of the completion.</param>
/// <param name="Timestamp">When the completion finished.</param>
public sealed record ChatCompleted(
    Guid ConversationId,
    Guid MessageId,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    TimeSpan Duration,
    DateTimeOffset Timestamp);
