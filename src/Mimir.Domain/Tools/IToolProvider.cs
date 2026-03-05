namespace Mimir.Domain.Tools;

/// <summary>
/// Unified abstraction for providing tools to the LLM — whether sourced
/// from legacy plugins, MCP servers, or any future tool source.
/// </summary>
public interface IToolProvider
{
    /// <summary>Returns all tools currently available from this provider.</summary>
    Task<IReadOnlyList<ToolDefinition>> GetAvailableToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>Executes a tool by name, passing arguments as a JSON string.</summary>
    Task<ToolResult> ExecuteToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default);
}
