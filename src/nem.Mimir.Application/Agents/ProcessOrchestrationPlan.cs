using nem.Contracts.Agents;
using nem.Contracts.Inference;
using nem.Mimir.Application.Agents.Selection;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Agents;

public sealed class ProcessOrchestrationPlan : IOrchestrationPlan
{
    private static readonly IReadOnlyDictionary<string, AgentCoordinationStrategy> DefaultStrategies =
        new Dictionary<string, AgentCoordinationStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            ["parallel"] = AgentCoordinationStrategy.Parallel,
            ["hierarchical"] = AgentCoordinationStrategy.Hierarchical,
            ["tiered"] = AgentCoordinationStrategy.Tiered,
        };

    private readonly IReadOnlyDictionary<string, AgentCoordinationStrategy> _strategyByName;

    public ProcessOrchestrationPlan(
        int defaultMaxTurns,
        SelectionProcessDefinition selectionProcess,
        TierConfiguration tierConfiguration,
        IReadOnlyDictionary<string, AgentCoordinationStrategy>? strategyByName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defaultMaxTurns);

        DefaultMaxTurns = defaultMaxTurns;
        SelectionProcess = selectionProcess ?? throw new ArgumentNullException(nameof(selectionProcess));
        TierConfiguration = tierConfiguration ?? throw new ArgumentNullException(nameof(tierConfiguration));
        _strategyByName = strategyByName ?? DefaultStrategies;
    }

    public int DefaultMaxTurns { get; }

    public SelectionProcessDefinition SelectionProcess { get; }

    public TierConfiguration TierConfiguration { get; }

    public double EscalationThreshold => TierConfiguration.EscalationThreshold;

    public AgentCoordinationStrategy ResolveStrategy(AgentTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (task.Context is null
            || !task.Context.TryGetValue("strategy", out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return AgentCoordinationStrategy.Sequential;
        }

        return _strategyByName.TryGetValue(raw.Trim(), out var strategy)
            ? strategy
            : AgentCoordinationStrategy.Sequential;
    }

    public InferenceTier ResolveEntryTier(AgentTask task)
        => TierConfiguration.ResolveEntryTier(task);

    public InferenceTier ResolveTierForAgent(string agentName)
        => TierConfiguration.ResolveTierForAgent(agentName);

    public string ResolveModel(InferenceTier tier)
        => TierConfiguration.GetModel(tier);

    public InferenceTier? GetNextTier(InferenceTier currentTier)
        => TierConfiguration.GetNextTier(currentTier);
}
