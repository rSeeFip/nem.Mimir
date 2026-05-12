using nem.Contracts.Agents;

namespace nem.Mimir.Application.Agents.Selection;

public sealed record SelectionContext(
    AgentTask Task,
    IReadOnlyList<ScoredAgent> Candidates,
    SelectionProcessDefinition ProcessDefinition,
    IReadOnlyDictionary<string, object?>? State = null)
{
    public SelectionContext WithCandidates(IReadOnlyList<ScoredAgent> candidates)
    {
        return this with { Candidates = candidates };
    }

    public SelectionContext SetState(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var updated = State is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(State, StringComparer.OrdinalIgnoreCase);

        updated[key] = value;
        return this with { State = updated };
    }
}
