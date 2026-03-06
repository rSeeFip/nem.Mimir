namespace Mimir.Infrastructure.Mcp;

using Mimir.Domain.Tools;

public sealed class McpToolAdapter(McpClientManager manager) : IToolProvider
{
    public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default)
        => manager.ListToolsAsync(cancellationToken);

    public Task<ToolInvocationResult> InvokeToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
        => manager.InvokeToolAsync(toolName, arguments, cancellationToken);
}
