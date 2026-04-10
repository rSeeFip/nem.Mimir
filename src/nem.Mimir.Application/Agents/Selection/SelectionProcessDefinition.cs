namespace nem.Mimir.Application.Agents.Selection;

public sealed class SelectionProcessDefinition
{
    public static SelectionProcessDefinition Default { get; } = new(
        [
            SelectionStepDefinition.Quality(),
            SelectionStepDefinition.Bm25(),
            SelectionStepDefinition.Embedding(),
        ],
        SelectionFallbackDefinition.Default);

    private readonly IReadOnlyDictionary<string, SelectionStepDefinition> _stepsByName;

    public SelectionProcessDefinition(
        IReadOnlyList<SelectionStepDefinition> steps,
        SelectionFallbackDefinition? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count == 0)
        {
            throw new ArgumentException("At least one selection step definition is required.", nameof(steps));
        }

        Steps = steps;
        Fallback = fallback ?? SelectionFallbackDefinition.Default;
        _stepsByName = steps.ToDictionary(step => step.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<SelectionStepDefinition> Steps { get; }

    public SelectionFallbackDefinition Fallback { get; }

    public SelectionStepDefinition GetStep(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _stepsByName.TryGetValue(name, out var definition)
            ? definition
            : throw new KeyNotFoundException($"No selection step definition is configured for '{name}'.");
    }
}
