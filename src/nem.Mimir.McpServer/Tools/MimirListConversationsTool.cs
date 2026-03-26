using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Queries;
using Wolverine;

namespace nem.Mimir.McpServer.Tools;

/// <summary>
/// MCP tool that lists conversations for the current user with pagination.
/// </summary>
[McpServerToolType]
public static class MimirListConversationsTool
{
    [McpServerTool(Name = "mimir_list_conversations"), Description("List conversations for the current user.")]
    public static async Task<string> ListConversationsAsync(
        [Description("The page number to retrieve (1-based, default 1).")] int pageNumber,
        [Description("The number of conversations per page (1-50, default 20).")] int pageSize,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        var query = new GetConversationsByUserQuery(
            pageNumber <= 0 ? 1 : pageNumber,
            pageSize is <= 0 or > 50 ? 20 : pageSize);
        var result = await messageBus.InvokeAsync<PaginatedList<ConversationListDto>>(query, cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }
}
