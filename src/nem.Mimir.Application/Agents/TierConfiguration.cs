using nem.Contracts.Agents;
using nem.Contracts.Inference;

namespace nem.Mimir.Application.Agents;

public sealed class TierConfiguration
{
    private static readonly IReadOnlyDictionary<InferenceTier, string> DefaultModels =
        new Dictionary<InferenceTier, string>
        {
            [InferenceTier.Router] = "phi-4-mini",
            [InferenceTier.Validation] = "phi-4-mini",
            [InferenceTier.Processing] = "gpt-4o-mini",
            [InferenceTier.Analysis] = "gpt-4o",
            [InferenceTier.Escalation] = "human-review",
        };

    public IDictionary<InferenceTier, string> ModelByTier { get; } =
        new Dictionary<InferenceTier, string>(DefaultModels);

    public IDictionary<string, InferenceTier> AgentTierAssignments { get; } =
        new Dictionary<string, InferenceTier>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<AgentTaskType, InferenceTier> EntryTierByTaskType { get; } =
        new Dictionary<AgentTaskType, InferenceTier>
        {
            [AgentTaskType.Explore] = InferenceTier.Router,
            [AgentTaskType.Custom] = InferenceTier.Router,
            [AgentTaskType.Research] = InferenceTier.Processing,
            [AgentTaskType.Execute] = InferenceTier.Processing,
            [AgentTaskType.Analyze] = InferenceTier.Analysis,
        };

    public string GetModel(InferenceTier tier)
    {
        return ModelByTier.TryGetValue(tier, out var model)
            ? model
            : DefaultModels[tier];
    }
}
