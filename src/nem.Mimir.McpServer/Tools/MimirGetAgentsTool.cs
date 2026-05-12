using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using nem.Contracts.Agents;

namespace nem.Mimir.McpServer.Tools;

/// <summary>
/// MCP tool that lists all registered specialist agents.
/// </summary>
[McpServerToolType]
public static class MimirGetAgentsTool
{
    [McpServerTool(Name = "mimir_get_agents"), Description("List all registered specialist agents and their capabilities.")]
    public static async Task<string> GetAgentsAsync(
        IAgentOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var agents = await orchestrator.ListAgentsAsync(cancellationToken);
        var summaries = agents.Select(a => new
        {
            a.Name,
            a.Description,
            Capabilities = a.Capabilities.Select(c => c.ToString()).ToList()
        });
        return JsonSerializer.Serialize(summaries, JsonOptions.Default);
    }
}
