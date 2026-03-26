using nem.Mimir.Domain.Tools;

namespace nem.Mimir.Application.Mcp;

/// <summary>
/// Defines persona-based access filtering for MCP tools.
/// Implementations may consult OPA, configuration, or other policy engines
/// to determine which tools a given persona is allowed to access.
/// </summary>
public interface IPersonaToolFilter
{
    /// <summary>
    /// Filters the provided tools based on the specified persona's access rules.
    /// </summary>
    /// <param name="tools">The full list of available tools.</param>
    /// <param name="persona">The persona identifier to filter for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The subset of tools the persona is allowed to access.</returns>
    Task<IReadOnlyList<ToolDefinition>> FilterAsync(
        IReadOnlyList<ToolDefinition> tools,
        string persona,
        CancellationToken cancellationToken = default);
}
