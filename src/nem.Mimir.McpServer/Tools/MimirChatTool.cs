using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using Wolverine;

namespace nem.Mimir.McpServer.Tools;

/// <summary>
/// MCP tool that sends a message to an existing Mimir conversation and returns the LLM response.
/// </summary>
[McpServerToolType]
public static class MimirChatTool
{
    [McpServerTool(Name = "mimir_chat"), Description("Send a message to an existing Mimir conversation and get the AI response.")]
    public static async Task<string> ChatAsync(
        [Description("The conversation ID to send the message to.")] Guid conversationId,
        [Description("The message content to send.")] string content,
        [Description("Optional LLM model identifier (e.g. 'phi-4-mini').")] string? model,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var command = new SendMessageCommand(conversationId, content, model);
        var result = await messageBus.InvokeAsync<MessageDto>(command, cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }
}
