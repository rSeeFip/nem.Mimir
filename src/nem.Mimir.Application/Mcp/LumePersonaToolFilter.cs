using System.Linq;
using Microsoft.Extensions.Options;
using nem.Mimir.Domain.Tools;

namespace nem.Mimir.Application.Mcp;

public sealed class LumePersonaToolFilter : IPersonaToolFilter
{
    private readonly PersonaToolFilterOptions _options;

    public LumePersonaToolFilter(IOptions<PersonaToolFilterOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<IReadOnlyList<ToolDefinition>> FilterAsync(
        IReadOnlyList<ToolDefinition> tools,
        string persona,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentException.ThrowIfNullOrWhiteSpace(persona);

        if (!_options.Personas.TryGetValue(persona, out var personaConfig))
        {
            return Task.FromResult(tools);
        }

        var includedServers = new HashSet<string>(personaConfig.IncludedServerNames ?? [], StringComparer.OrdinalIgnoreCase);
        var includedPrefixes = personaConfig.IncludedToolNamePrefixes ?? [];
        var excludedToolNames = new HashSet<string>(personaConfig.ExcludedToolNames ?? [], StringComparer.OrdinalIgnoreCase);

        var filtered = tools.Where(tool => IsAllowed(tool, includedServers, includedPrefixes, excludedToolNames)).ToArray();
        return Task.FromResult<IReadOnlyList<ToolDefinition>>(filtered);
    }

    private static bool IsAllowed(
        ToolDefinition tool,
        ISet<string> includedServers,
        IReadOnlyList<string> includedPrefixes,
        ISet<string> excludedToolNames)
    {
        if (excludedToolNames.Contains(tool.Name))
        {
            return false;
        }

        if (includedServers.Contains(tool.ServerName))
        {
            return true;
        }

        if (includedPrefixes.Any(prefix => tool.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return true;
    }
}
