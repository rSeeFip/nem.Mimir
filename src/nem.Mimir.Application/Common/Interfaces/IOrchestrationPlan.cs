using nem.Contracts.Agents;
using nem.Contracts.Inference;
using nem.Mimir.Application.Agents;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IOrchestrationPlan
{
    int DefaultMaxTurns { get; }

    double EscalationThreshold { get; }

    AgentCoordinationStrategy ResolveStrategy(AgentTask task);

    InferenceTier ResolveEntryTier(AgentTask task);

    InferenceTier ResolveTierForAgent(string agentName);

    string ResolveModel(InferenceTier tier);

    InferenceTier? GetNextTier(InferenceTier currentTier);
}
