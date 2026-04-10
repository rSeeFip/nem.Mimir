namespace nem.Mimir.Application.Agents.Selection;

public sealed record SelectionFallbackDefinition(
    IReadOnlyList<string> PreferredAgentNamePatterns,
    bool UseAlphabeticalFallback = true)
{
    public static SelectionFallbackDefinition Default { get; } = new(["general"]);
}
