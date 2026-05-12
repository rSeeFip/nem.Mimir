using nem.Contracts.Agents;
using nem.Contracts.Inference;
using nem.Mimir.Application.Agents;
using nem.Mimir.Application.Agents.Selection;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IOrchestrationPlan
{
    int DefaultMaxTurns { get; }

    SelectionProcessDefinition SelectionProcess { get; }

    TierConfiguration TierConfiguration { get; }

    double EscalationThreshold { get; }

    AgentCoordinationStrategy ResolveStrategy(AgentTask task);

    InferenceTier ResolveEntryTier(AgentTask task);

    InferenceTier ResolveTierForAgent(string agentName);

    string ResolveModel(InferenceTier tier);

    InferenceTier? GetNextTier(InferenceTier currentTier);

    WorkflowBackedOrchestrationDefinition? ResolveWorkflowExecution(AgentTask task);
}
