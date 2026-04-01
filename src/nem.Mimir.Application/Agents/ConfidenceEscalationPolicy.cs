using nem.Contracts.Inference;

namespace nem.Mimir.Application.Agents;

public sealed class ConfidenceEscalationPolicy
{
    private const double DefaultThreshold = 0.7d;

    public double Threshold { get; set; } = DefaultThreshold;

    public bool ShouldEscalate(double confidence) => confidence < Threshold;

    public InferenceTier? GetNextTier(InferenceTier currentTier)
    {
        return currentTier switch
        {
            InferenceTier.Router => InferenceTier.Validation,
            InferenceTier.Validation => InferenceTier.Processing,
            InferenceTier.Processing => InferenceTier.Analysis,
            InferenceTier.Analysis => InferenceTier.Escalation,
            InferenceTier.Escalation => null,
            _ => null,
        };
    }
}
