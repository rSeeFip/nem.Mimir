namespace Mimir.Domain.Tools;

public interface IToolProvider
{
    Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default);

    Task<ToolInvocationResult> InvokeToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default);
}
