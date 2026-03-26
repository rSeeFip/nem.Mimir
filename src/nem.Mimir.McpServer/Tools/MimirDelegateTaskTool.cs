using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using nem.Contracts.Agents;

namespace nem.Mimir.McpServer.Tools;

/// <summary>
/// MCP tool that delegates a task to a specialist agent via the agent orchestrator.
/// </summary>
[McpServerToolType]
public static class MimirDelegateTaskTool
{
    [McpServerTool(Name = "mimir_delegate_task"), Description("Delegate a task to a specialist agent for execution.")]
    public static async Task<string> DelegateTaskAsync(
        [Description("The type of task: Explore, Research, Analyze, Execute, or Custom.")] string taskType,
        [Description("The task prompt or description.")] string prompt,
        [Description("Optional priority level (default 0).")] int priority,
        [Description("Optional timeout in seconds (default 300).")] int timeoutSeconds,
        IAgentOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AgentTaskType>(taskType, ignoreCase: true, out var parsedType))
        {
            parsedType = AgentTaskType.Custom;
        }

        var task = new AgentTask(
            Id: Guid.NewGuid().ToString(),
            Type: parsedType,
            Prompt: prompt,
            Priority: priority,
            TimeoutSeconds: timeoutSeconds <= 0 ? 300 : timeoutSeconds);

        var result = await orchestrator.DispatchAsync(task, cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }
}
