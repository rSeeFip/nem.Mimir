using nem.Mimir.Application.Common.Interfaces;
using nem.Contracts.Inference;

namespace nem.Mimir.Application.Agents;

public sealed class ConfidenceEscalationPolicy
{
    public bool ShouldEscalate(double confidence, IOrchestrationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return confidence < plan.EscalationThreshold;
    }

    public InferenceTier? GetNextTier(InferenceTier currentTier, IOrchestrationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.GetNextTier(currentTier);
    }
}
