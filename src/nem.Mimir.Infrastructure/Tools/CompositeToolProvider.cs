using nem.Mimir.Domain.Tools;

namespace nem.Mimir.Infrastructure.Tools;

internal sealed class CompositeToolProvider : IToolProvider
{
    private readonly IReadOnlyList<IToolProvider> _providers;

    public CompositeToolProvider(IEnumerable<IToolProvider> providers)
    {
        _providers = providers.ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<ToolDefinition>> GetAvailableToolsAsync(CancellationToken cancellationToken = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tools = new List<ToolDefinition>();

        foreach (var provider in _providers)
        {
            var providerTools = await provider.GetAvailableToolsAsync(cancellationToken);
            foreach (var tool in providerTools)
            {
                if (seen.Add(tool.Name))
                    tools.Add(tool);
            }
        }

        return tools.AsReadOnly();
    }

    public async Task<ToolResult> ExecuteToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            var tools = await provider.GetAvailableToolsAsync(cancellationToken);
            if (tools.Any(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase)))
                return await provider.ExecuteToolAsync(toolName, argumentsJson, cancellationToken);
        }

        return ToolResult.Failure($"Tool '{toolName}' not found in any provider");
    }
}
