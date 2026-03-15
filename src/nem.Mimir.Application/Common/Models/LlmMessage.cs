namespace nem.Mimir.Application.Common.Models;

/// <summary>
/// Represents a message in the LLM conversation context.
/// Provider-agnostic representation of a chat message.
/// </summary>
/// <param name="Role">The role of the message sender (e.g., "user", "assistant", "system").</param>
/// <param name="Content">The text content of the message.</param>
public sealed record LlmMessage(string Role, string Content);
