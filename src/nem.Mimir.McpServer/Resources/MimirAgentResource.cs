using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using nem.Contracts.Agents;

namespace nem.Mimir.McpServer.Resources;

/// <summary>
/// MCP resource that exposes a Mimir specialist agent by name.
/// </summary>
[McpServerResourceType]
public static class MimirAgentResource
{
    [McpServerResource(UriTemplate = "mimir://agents/{agentName}", Name = "Mimir Agent"),
     Description("Retrieve a specialist agent's details by name.")]
    public static async Task<string> GetAgentAsync(
        [Description("The agent name.")] string agentName,
        IAgentOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        var agent = await orchestrator.GetAgentAsync(agentName, cancellationToken);
        if (agent is null)
        {
            return JsonSerializer.Serialize(new { error = $"Agent '{agentName}' not found." }, JsonOptions.Default);
        }

        var summary = new
        {
            agent.Name,
            agent.Description,
            Capabilities = agent.Capabilities.Select(c => c.ToString()).ToList()
        };
        return JsonSerializer.Serialize(summary, JsonOptions.Default);
    }
}
