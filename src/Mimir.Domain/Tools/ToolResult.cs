namespace Mimir.Domain.Tools;

/// <summary>
/// Represents the outcome of a tool execution.
/// </summary>
public sealed record ToolResult(bool IsSuccess, string Content, string? ErrorMessage = null)
{
    /// <summary>Creates a successful result with the given content.</summary>
    public static ToolResult Success(string content) => new(true, content);

    /// <summary>Creates a failure result with the given error message.</summary>
    public static ToolResult Failure(string error) => new(false, string.Empty, error);
}
