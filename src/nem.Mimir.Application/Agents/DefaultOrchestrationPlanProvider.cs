using nem.Contracts.Agents;
using nem.Contracts.Inference;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Agents;

public sealed class DefaultOrchestrationPlanProvider : IOrchestrationPlanProvider
{
    private static readonly IOrchestrationPlan DefaultPlan = new DefaultOrchestrationPlan();

    public IOrchestrationPlan ResolvePlan(AgentExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return DefaultPlan;
    }

    private sealed class DefaultOrchestrationPlan : IOrchestrationPlan
    {
        private static readonly IReadOnlyDictionary<InferenceTier, string> ModelByTier =
            new Dictionary<InferenceTier, string>
            {
                [InferenceTier.Router] = "phi-4-mini",
                [InferenceTier.Validation] = "phi-4-mini",
                [InferenceTier.Processing] = "gpt-4o-mini",
                [InferenceTier.Analysis] = "gpt-4o",
                [InferenceTier.Escalation] = "human-review",
            };

        private static readonly IReadOnlyDictionary<AgentTaskType, InferenceTier> EntryTierByTaskType =
            new Dictionary<AgentTaskType, InferenceTier>
            {
                [AgentTaskType.Explore] = InferenceTier.Router,
                [AgentTaskType.Custom] = InferenceTier.Router,
                [AgentTaskType.Research] = InferenceTier.Processing,
                [AgentTaskType.Execute] = InferenceTier.Processing,
                [AgentTaskType.Analyze] = InferenceTier.Analysis,
            };

        private static readonly IReadOnlyDictionary<string, AgentCoordinationStrategy> StrategyByName =
            new Dictionary<string, AgentCoordinationStrategy>(StringComparer.OrdinalIgnoreCase)
            {
                ["parallel"] = AgentCoordinationStrategy.Parallel,
                ["hierarchical"] = AgentCoordinationStrategy.Hierarchical,
                ["tiered"] = AgentCoordinationStrategy.Tiered,
            };

        private static readonly IReadOnlyList<(InferenceTier Tier, string[] Keywords)> AgentTierRules =
        [
            (InferenceTier.Router, ["router", "explore"]),
            (InferenceTier.Validation, ["valid", "schema"]),
            (InferenceTier.Analysis, ["analy", "reason"]),
            (InferenceTier.Escalation, ["escal", "human"]),
        ];

        private static readonly IReadOnlyList<InferenceTier> EscalationPath =
        [
            InferenceTier.Router,
            InferenceTier.Validation,
            InferenceTier.Processing,
            InferenceTier.Analysis,
            InferenceTier.Escalation,
        ];

        public int DefaultMaxTurns => 10;

        public double EscalationThreshold => 0.7d;

        public AgentCoordinationStrategy ResolveStrategy(AgentTask task)
        {
            ArgumentNullException.ThrowIfNull(task);

            if (task.Context is null
                || !task.Context.TryGetValue("strategy", out var raw)
                || string.IsNullOrWhiteSpace(raw))
            {
                return AgentCoordinationStrategy.Sequential;
            }

            return StrategyByName.TryGetValue(raw.Trim(), out var strategy)
                ? strategy
                : AgentCoordinationStrategy.Sequential;
        }

        public InferenceTier ResolveEntryTier(AgentTask task)
        {
            ArgumentNullException.ThrowIfNull(task);

            if (task.Context is not null
                && task.Context.TryGetValue("inferenceTier", out var configuredTier)
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

        public string ResolveModel(InferenceTier tier)
        {
            return ModelByTier.TryGetValue(tier, out var model)
                ? model
                : throw new KeyNotFoundException($"No model is configured for inference tier '{tier}'.");
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
}
