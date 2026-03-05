namespace Mimir.Infrastructure.Tools;

using Mimir.Domain.McpServers;
using Mimir.Domain.Tools;

internal sealed class ToolWhitelistFilter(
    IToolProvider inner,
    IToolWhitelistService whitelistService,
    Guid mcpServerConfigId) : IToolProvider
{
    public async Task<IReadOnlyList<ToolDefinition>> GetAvailableToolsAsync(
        CancellationToken cancellationToken = default)
    {
        var allTools = await inner.GetAvailableToolsAsync(cancellationToken).ConfigureAwait(false);

        var allowed = new List<ToolDefinition>();
        foreach (var tool in allTools)
        {
            if (await whitelistService.IsToolAllowedAsync(mcpServerConfigId, tool.Name, cancellationToken)
                    .ConfigureAwait(false))
            {
                allowed.Add(tool);
            }
        }

        return allowed;
    }

    public async Task<ToolResult> ExecuteToolAsync(
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        if (!await whitelistService.IsToolAllowedAsync(mcpServerConfigId, toolName, cancellationToken)
                .ConfigureAwait(false))
        {
            return ToolResult.Failure("Tool not whitelisted");
        }

        if (!whitelistService.ValidateArguments(argumentsJson))
        {
            return ToolResult.Failure("Argument validation failed");
        }

        return await inner.ExecuteToolAsync(toolName, argumentsJson, cancellationToken).ConfigureAwait(false);
    }
}
