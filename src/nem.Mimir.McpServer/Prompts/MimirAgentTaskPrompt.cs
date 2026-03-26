using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace nem.Mimir.McpServer.Prompts;

/// <summary>
/// MCP prompt that generates a structured agent task prompt for Mimir specialist agents.
/// </summary>
[McpServerPromptType]
public static class MimirAgentTaskPrompt
{
    [McpServerPrompt(Name = "mimir_agent_task"), Description("Generate a structured prompt for delegating a task to a Mimir specialist agent.")]
    public static IEnumerable<ChatMessage> GetPrompt(
        [Description("The type of task: Explore, Research, Analyze, Execute, or Custom.")] string taskType,
        [Description("Detailed description of what the agent should accomplish.")] string taskDescription,
        [Description("Optional additional context or constraints.")] string? context)
    {
        var systemMessage = """
            You are a task delegation assistant for Mimir's agent system.
            Your role is to formulate clear, actionable tasks for specialist agents.
            Each agent has specific capabilities: code exploration, knowledge retrieval,
            deep analysis, code execution, web research, data processing, and tool invocation.
            """;

        yield return new ChatMessage(ChatRole.System, systemMessage);

        var userContent = $"""
            Please create a well-structured task for a specialist agent.

            Task Type: {taskType}
            Task Description: {taskDescription}
            {(context is not null ? $"Additional Context: {context}" : "")}

            Provide:
            1. A clear, specific prompt for the agent
            2. Expected output format
            3. Any constraints or boundaries the agent should respect
            """;

        yield return new ChatMessage(ChatRole.User, userContent);
    }
}
