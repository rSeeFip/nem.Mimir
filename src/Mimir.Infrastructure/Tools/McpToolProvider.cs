namespace Mimir.Infrastructure.Tools;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Mimir.Domain.McpServers;
using Mimir.Domain.Tools;

/// <summary>
/// Bridges MCP server tools into the unified <see cref="IToolProvider"/> abstraction.
/// Aggregates tools from all connected MCP servers, handles name collisions by
/// namespacing with the server name, and routes executions to the correct server.
/// </summary>
internal sealed class McpToolProvider : IToolProvider
{
    private readonly IMcpClientManager _clientManager;
    private readonly ILogger<McpToolProvider> _logger;

    /// <summary>
    /// Maps exposed tool name → (ServerId, OriginalToolName) for execution routing.
    /// Refreshed on each <see cref="GetAvailableToolsAsync"/> call.
    /// </summary>
    private readonly ConcurrentDictionary<string, (Guid ServerId, string OriginalName)> _toolMapping = new(StringComparer.OrdinalIgnoreCase);

    public McpToolProvider(IMcpClientManager clientManager, ILogger<McpToolProvider> logger)
    {
        _clientManager = clientManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ToolDefinition>> GetAvailableToolsAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _clientManager.GetConnectedServersAsync(cancellationToken);
        var toolNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var serverTools = new List<(McpServerConfig Server, IReadOnlyList<ToolDefinition> Tools)>();

        // First pass: collect all tools and detect name collisions
        foreach (var server in servers)
        {
            try
            {
                var tools = await _clientManager.GetServerToolsAsync(server.Id, cancellationToken);
                serverTools.Add((server, tools));

                foreach (var tool in tools)
                {
                    toolNameCounts[tool.Name] = toolNameCounts.GetValueOrDefault(tool.Name) + 1;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get tools from MCP server {ServerName} ({ServerId})", server.Name, server.Id);
            }
        }

        // Second pass: build definitions with namespaced names for collisions, and cache the mapping
        var newMapping = new ConcurrentDictionary<string, (Guid ServerId, string OriginalName)>(StringComparer.OrdinalIgnoreCase);
        var allTools = new List<ToolDefinition>();

        foreach (var (server, tools) in serverTools)
        {
            foreach (var tool in tools)
            {
                var exposedName = toolNameCounts[tool.Name] > 1
                    ? $"{server.Name}_{tool.Name}"
                    : tool.Name;

                allTools.Add(tool with { Name = exposedName });
                newMapping[exposedName] = (server.Id, tool.Name);
            }
        }

        // Atomically swap the mapping cache
        _toolMapping.Clear();
        foreach (var kvp in newMapping)
        {
            _toolMapping[kvp.Key] = kvp.Value;
        }

        return allTools.AsReadOnly();
    }

    public async Task<ToolResult> ExecuteToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        // Try cached mapping first (populated by GetAvailableToolsAsync)
        if (_toolMapping.TryGetValue(toolName, out var mapping))
        {
            try
            {
                return await _clientManager.ExecuteToolAsync(mapping.ServerId, mapping.OriginalName, argumentsJson, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute tool {ToolName} on server {ServerId}", toolName, mapping.ServerId);
                return ToolResult.Failure($"Tool execution failed: {ex.Message}");
            }
        }

        // Fallback: scan servers directly (mapping may be stale or not yet populated)
        var servers = await _clientManager.GetConnectedServersAsync(cancellationToken);
        foreach (var server in servers)
        {
            try
            {
                var tools = await _clientManager.GetServerToolsAsync(server.Id, cancellationToken);

                // Check for direct name match
                if (tools.Any(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase)))
                {
                    return await _clientManager.ExecuteToolAsync(server.Id, toolName, argumentsJson, cancellationToken);
                }

                // Check for namespaced match (serverName_toolName)
                var prefix = server.Name + "_";
                if (toolName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var originalName = toolName[prefix.Length..];
                    if (tools.Any(t => string.Equals(t.Name, originalName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return await _clientManager.ExecuteToolAsync(server.Id, originalName, argumentsJson, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking server {ServerName} for tool {ToolName}", server.Name, toolName);
            }
        }

        return ToolResult.Failure($"Tool '{toolName}' not found on any connected MCP server");
    }
}
