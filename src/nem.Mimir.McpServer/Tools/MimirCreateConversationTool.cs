using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using Wolverine;

namespace nem.Mimir.McpServer.Tools;

/// <summary>
/// MCP tool that creates a new Mimir conversation.
/// </summary>
[McpServerToolType]
public static class MimirCreateConversationTool
{
    [McpServerTool(Name = "mimir_create_conversation"), Description("Create a new Mimir conversation.")]
    public static async Task<string> CreateConversationAsync(
        [Description("The title for the new conversation.")] string title,
        [Description("Optional system prompt to set the conversation context.")] string? systemPrompt,
        [Description("Optional LLM model identifier to use for this conversation.")] string? model,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var command = new CreateConversationCommand(title, systemPrompt, model);
        var result = await messageBus.InvokeAsync<ConversationDto>(command, cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }
}
