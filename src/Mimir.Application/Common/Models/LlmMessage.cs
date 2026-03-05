namespace Mimir.Application.Common.Models;

/// <summary>
/// Represents a tool call requested by the LLM.
/// Provider-agnostic representation of a function call.
/// </summary>
/// <param name="Id">The unique identifier for this tool call.</param>
/// <param name="FunctionName">The name of the function to call.</param>
/// <param name="ArgumentsJson">The JSON-encoded arguments for the function.</param>
public sealed record LlmToolCall(string Id, string FunctionName, string ArgumentsJson);

/// <summary>
/// Represents a message in the LLM conversation context.
/// Provider-agnostic representation of a chat message.
/// </summary>
/// <param name="Role">The role of the message sender (e.g., "user", "assistant", "system", "tool").</param>
/// <param name="Content">The text content of the message.</param>
public sealed record LlmMessage(string Role, string Content)
{
    /// <summary>
    /// The tool call ID this message is responding to (for role="tool" messages).
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// The name of the function/tool (used with tool messages).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Tool calls requested by the assistant (for role="assistant" messages with tool_calls).
    /// </summary>
    public IReadOnlyList<LlmToolCall>? ToolCalls { get; init; }
}
