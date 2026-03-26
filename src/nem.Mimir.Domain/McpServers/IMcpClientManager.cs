namespace nem.Mimir.Domain.McpServers;

using nem.Mimir.Domain.Tools;

/// <summary>
/// Manages MCP client connections: connect, disconnect, list, execute tools, resources, prompts, and health-check.
/// </summary>
public interface IMcpClientManager
{
    /// <summary>Connects to an MCP server using the supplied configuration.</summary>
    Task ConnectAsync(McpServerConfig config, CancellationToken ct = default);

    /// <summary>Disconnects and disposes the MCP client for the given server.</summary>
    Task DisconnectAsync(Guid serverId, CancellationToken ct = default);

    /// <summary>Returns configurations of all currently connected servers.</summary>
    Task<IReadOnlyList<McpServerConfig>> GetConnectedServersAsync(CancellationToken ct = default);

    /// <summary>Lists tools exposed by a connected MCP server.</summary>
    Task<IReadOnlyList<ToolDefinition>> GetServerToolsAsync(Guid serverId, CancellationToken ct = default);

    /// <summary>Executes a tool on a connected MCP server.</summary>
    Task<ToolResult> ExecuteToolAsync(Guid serverId, string toolName, string? argumentsJson, CancellationToken ct = default);

    /// <summary>Pings the server to verify the connection is healthy.</summary>
    Task<bool> HealthCheckAsync(Guid serverId, CancellationToken ct = default);

    // ── Resources primitive ──────────────────────────────────────────────

    /// <summary>Lists resources exposed by a connected MCP server.</summary>
    Task<IReadOnlyList<McpResourceDefinition>> ListResourcesAsync(Guid serverId, CancellationToken ct = default);

    /// <summary>Lists resource templates exposed by a connected MCP server.</summary>
    Task<IReadOnlyList<McpResourceTemplateDefinition>> ListResourceTemplatesAsync(Guid serverId, CancellationToken ct = default);

    /// <summary>Reads a resource from a connected MCP server.</summary>
    Task<IReadOnlyList<McpResourceContent>> ReadResourceAsync(Guid serverId, string uri, CancellationToken ct = default);

    // ── Prompts primitive ────────────────────────────────────────────────

    /// <summary>Lists prompts exposed by a connected MCP server.</summary>
    Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(Guid serverId, CancellationToken ct = default);

    /// <summary>Gets a prompt from a connected MCP server with the given arguments.</summary>
    Task<McpPromptResult> GetPromptAsync(Guid serverId, string promptName, IReadOnlyDictionary<string, string>? arguments = null, CancellationToken ct = default);
}
