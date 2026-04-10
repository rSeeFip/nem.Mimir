using nem.Contracts.Agents;
using nem.Contracts.Inference;

namespace nem.Mimir.Application.Agents;

public sealed class TierConfiguration
{
    private const string InferenceTierContextKey = "inferenceTier";

    public TierConfiguration(
        IReadOnlyDictionary<InferenceTier, string>? modelByTier = null,
        IReadOnlyDictionary<string, InferenceTier>? agentTierAssignments = null,
        IReadOnlyDictionary<AgentTaskType, InferenceTier>? entryTierByTaskType = null,
        IReadOnlyList<AgentTierRule>? agentTierRules = null,
        IReadOnlyList<InferenceTier>? escalationPath = null,
        double escalationThreshold = 0.7d)
    {
        if (escalationThreshold is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(escalationThreshold), "Escalation threshold must be between 0 and 1.");
        }

        ModelByTier = modelByTier is null
            ? new Dictionary<InferenceTier, string>(DefaultModels)
            : new Dictionary<InferenceTier, string>(modelByTier);

        AgentTierAssignments = agentTierAssignments is null
            ? new Dictionary<string, InferenceTier>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, InferenceTier>(agentTierAssignments, StringComparer.OrdinalIgnoreCase);

        EntryTierByTaskType = entryTierByTaskType is null
            ? new Dictionary<AgentTaskType, InferenceTier>(DefaultEntryTiers)
            : new Dictionary<AgentTaskType, InferenceTier>(entryTierByTaskType);

        AgentTierRules = agentTierRules ?? DefaultAgentTierRules;
        EscalationPath = escalationPath ?? DefaultEscalationPath;
        EscalationThreshold = escalationThreshold;
    }

    private static readonly IReadOnlyDictionary<InferenceTier, string> DefaultModels =
        new Dictionary<InferenceTier, string>
        {
            [InferenceTier.Router] = "phi-4-mini",
            [InferenceTier.Validation] = "phi-4-mini",
            [InferenceTier.Processing] = "gpt-4o-mini",
            [InferenceTier.Analysis] = "gpt-4o",
            [InferenceTier.Escalation] = "human-review",
        };

    private static readonly IReadOnlyDictionary<AgentTaskType, InferenceTier> DefaultEntryTiers =
        new Dictionary<AgentTaskType, InferenceTier>
        {
            [AgentTaskType.Explore] = InferenceTier.Router,
            [AgentTaskType.Custom] = InferenceTier.Router,
            [AgentTaskType.Research] = InferenceTier.Processing,
            [AgentTaskType.Execute] = InferenceTier.Processing,
            [AgentTaskType.Analyze] = InferenceTier.Analysis,
        };

    private static readonly IReadOnlyList<AgentTierRule> DefaultAgentTierRules =
    [
        new(InferenceTier.Router, ["router", "explore"]),
        new(InferenceTier.Validation, ["valid", "schema"]),
        new(InferenceTier.Analysis, ["analy", "reason"]),
        new(InferenceTier.Escalation, ["escal", "human"]),
    ];

    private static readonly IReadOnlyList<InferenceTier> DefaultEscalationPath =
    [
        InferenceTier.Router,
        InferenceTier.Validation,
        InferenceTier.Processing,
        InferenceTier.Analysis,
        InferenceTier.Escalation,
    ];

    public static TierConfiguration Default { get; } = new();

    public IReadOnlyDictionary<InferenceTier, string> ModelByTier { get; }

    public IReadOnlyDictionary<string, InferenceTier> AgentTierAssignments { get; }

    public IReadOnlyDictionary<AgentTaskType, InferenceTier> EntryTierByTaskType { get; }

    public IReadOnlyList<AgentTierRule> AgentTierRules { get; }

    public IReadOnlyList<InferenceTier> EscalationPath { get; }

    public double EscalationThreshold { get; }

    public string GetModel(InferenceTier tier)
    {
        return ModelByTier.TryGetValue(tier, out var model)
            ? model
            : throw new KeyNotFoundException($"No model is configured for inference tier '{tier}'.");
    }

    public InferenceTier ResolveEntryTier(AgentTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (task.Context is not null
            && task.Context.TryGetValue(InferenceTierContextKey, out var configuredTier)
            && Enum.TryParse<InferenceTier>(configuredTier, ignoreCase: true, out var explicitTier))
        {
            return explicitTier;
        }

        return EntryTierByTaskType.TryGetValue(task.Type, out var mapped)
            ? mapped
            : InferenceTier.Processing;
    }

    public InferenceTier ResolveTierForAgent(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return InferenceTier.Processing;
        }

        if (AgentTierAssignments.TryGetValue(agentName, out var assignedTier))
        {
            return assignedTier;
        }

        var normalized = agentName.Trim().ToLowerInvariant();

        foreach (var rule in AgentTierRules)
        {
            if (rule.Keywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
            {
                return rule.Tier;
            }
        }

        return InferenceTier.Processing;
    }

    public InferenceTier? GetNextTier(InferenceTier currentTier)
    {
        for (var i = 0; i < EscalationPath.Count; i++)
        {
            if (EscalationPath[i] == currentTier)
            {
                return i == EscalationPath.Count - 1
                    ? null
                    : EscalationPath[i + 1];
            }
        }

        return null;
    }
}

public sealed record AgentTierRule(InferenceTier Tier, IReadOnlyList<string> Keywords);
