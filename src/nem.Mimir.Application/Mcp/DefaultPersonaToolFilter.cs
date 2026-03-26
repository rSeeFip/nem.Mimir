using nem.Mimir.Domain.Tools;

namespace nem.Mimir.Application.Mcp;

/// <summary>
/// Default persona tool filter that allows all tools for any persona.
/// Replace with an OPA-backed implementation for production persona-based filtering.
/// </summary>
public sealed class DefaultPersonaToolFilter : IPersonaToolFilter
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ToolDefinition>> FilterAsync(
        IReadOnlyList<ToolDefinition> tools,
        string persona,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentException.ThrowIfNullOrWhiteSpace(persona);

        // Default implementation: all tools are accessible to all personas.
        // Override with OPA or rule-based filter for production use.
        return Task.FromResult(tools);
    }
}
