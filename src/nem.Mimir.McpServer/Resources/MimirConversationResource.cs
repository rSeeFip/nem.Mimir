using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Queries;
using Wolverine;

namespace nem.Mimir.McpServer.Resources;

/// <summary>
/// MCP resource that exposes a Mimir conversation by ID.
/// </summary>
[McpServerResourceType]
public static class MimirConversationResource
{
    [McpServerResource(UriTemplate = "mimir://conversations/{conversationId}", Name = "Mimir Conversation"),
     Description("Retrieve a Mimir conversation and its messages by ID.")]
    public static async Task<string> GetConversationAsync(
        [Description("The conversation ID.")] string conversationId,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var id = Guid.Parse(conversationId);
        var query = new GetConversationByIdQuery(id);
        var result = await messageBus.InvokeAsync<ConversationDto>(query, cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }
}
