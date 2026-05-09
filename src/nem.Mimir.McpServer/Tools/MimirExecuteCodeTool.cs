using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using nem.Mimir.Application.CodeExecution.Commands;
using Wolverine;

namespace nem.Mimir.McpServer.Tools;

/// <summary>
/// MCP tool that executes code in a sandboxed environment within a conversation context.
/// </summary>
[McpServerToolType]
public static class MimirExecuteCodeTool
{
    [McpServerTool(Name = "mimir_execute_code"), Description("Execute code in a sandboxed environment within a conversation.")]
    public static async Task<string> ExecuteCodeAsync(
        [Description("The programming language ('python' or 'javascript').")] string language,
        [Description("The source code to execute.")] string code,
        [Description("The conversation ID for execution context.")] Guid conversationId,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var command = new ExecuteCodeCommand(language, code, conversationId);
        var result = await messageBus.InvokeAsync<CodeExecutionResultDto>(command, cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }
}
