using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using Wolverine;

namespace nem.Mimir.McpServer.Tools;

/// <summary>
/// MCP tool that forks an existing conversation into a new branch.
/// </summary>
[McpServerToolType]
public static class MimirForkConversationTool
{
    [McpServerTool(Name = "mimir_fork_conversation"), Description("Fork an existing conversation into a new branch.")]
    public static async Task<string> ForkConversationAsync(
        [Description("The ID of the conversation to fork.")] Guid conversationId,
        [Description("The reason for forking the conversation.")] string forkReason,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var command = new ForkConversationCommand(conversationId, forkReason);
        var result = await messageBus.InvokeAsync<ConversationDto>(command, cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }
}
