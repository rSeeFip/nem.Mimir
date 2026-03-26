using nem.Mimir.Domain.Tools;

namespace nem.Mimir.Application.Mcp;

/// <summary>
/// Application-layer service for aggregating, filtering, and routing MCP tools
/// from multiple registered MCP servers. Provides persona-based access filtering
/// and event-driven cache invalidation on top of the underlying <see cref="IToolProvider"/>.
/// </summary>
public interface IMcpToolCatalogService
{
    /// <summary>
    /// Lists all available tools across all registered MCP servers,
    /// optionally filtered by persona access rules.
    /// </summary>
    /// <param name="persona">Optional persona identifier for tool access filtering. When null, all tools are returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The filtered list of available tool definitions.</returns>
    Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(string? persona = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a tool by name, routing the call to the appropriate MCP server.
    /// </summary>
    /// <param name="toolName">Fully qualified tool name (e.g., "servername.toolname").</param>
    /// <param name="arguments">Tool invocation arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result from the source MCP server.</returns>
    Task<ToolInvocationResult> InvokeToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches tools by name or description substring, optionally filtered by persona.
    /// </summary>
    /// <param name="query">Search query to match against tool name or description.</param>
    /// <param name="persona">Optional persona for access filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching tool definitions.</returns>
    Task<IReadOnlyList<ToolDefinition>> SearchToolsAsync(
        string query,
        string? persona = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists tools available from a specific MCP server, optionally filtered by persona.
    /// </summary>
    /// <param name="serverName">The MCP server name to filter by.</param>
    /// <param name="persona">Optional persona for access filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool definitions from the specified server.</returns>
    Task<IReadOnlyList<ToolDefinition>> ListToolsByServerAsync(
        string serverName,
        string? persona = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the tool catalog cache, forcing re-discovery on the next request.
    /// Typically triggered by MCP server registration/deregistration events.
    /// </summary>
    void InvalidateCache();
}
