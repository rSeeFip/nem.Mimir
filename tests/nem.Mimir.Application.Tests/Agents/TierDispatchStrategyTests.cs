using nem.Contracts.Agents;
using nem.Contracts.Inference;
using nem.Mimir.Application.Agents;
using nem.Mimir.Application.Common.Interfaces;
using Shouldly;
using AppSpecialistAgent = nem.Mimir.Application.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Tests.Agents;

public sealed class TierDispatchStrategyTests
{
    [Fact]
    public void ResolveEntryTier_ShouldMapTaskType()
    {
        var strategy = new TierDispatchStrategy();
        var plan = new TestOrchestrationPlan();

        var tier = strategy.ResolveEntryTier(new AgentTask("t1", AgentTaskType.Analyze, "analyze"), plan);

        tier.ShouldBe(InferenceTier.Analysis);
    }

    [Fact]
    public void ResolveEntryTier_ShouldRespectContextOverride()
    {
        var strategy = new TierDispatchStrategy();
        var plan = new TestOrchestrationPlan();
        var task = new AgentTask(
            "t2",
            AgentTaskType.Explore,
            "route",
            new Dictionary<string, string> { ["inferenceTier"] = "Processing" });

        var tier = strategy.ResolveEntryTier(task, plan);

        tier.ShouldBe(InferenceTier.Processing);
    }

    [Fact]
    public void SelectCandidatesForTier_ShouldFilterToMatchingTier()
    {
        var strategy = new TierDispatchStrategy();
        var plan = new TestOrchestrationPlan();
        var router = new TestAgent("Explore Agent");
        var analyze = new TestAgent("Analyze Agent");

        var selected = strategy.SelectCandidatesForTier([router, analyze], InferenceTier.Analysis, plan);

        selected.Count.ShouldBe(1);
        selected[0].Name.ShouldBe("Analyze Agent");
    }

    [Fact]
    public void EstimateConfidence_ShouldReadConfidenceFromArtifactsOrOutput()
    {
        var strategy = new TierDispatchStrategy();
        var fromArtifact = strategy.EstimateConfidence(
            new AgentResult("t3", "a", "Completed", "ignored", Artifacts: ["confidence=0.44"]));
        var fromOutput = strategy.EstimateConfidence(
            new AgentResult("t4", "a", "Completed", "confidence:0.81"));

        fromArtifact.ShouldBe(0.44d, 0.0001d);
        fromOutput.ShouldBe(0.81d, 0.0001d);
    }

    private sealed class TestOrchestrationPlan : IOrchestrationPlan
    {
        public int DefaultMaxTurns => 10;

        public double EscalationThreshold => 0.7d;

        public AgentCoordinationStrategy ResolveStrategy(AgentTask task) => AgentCoordinationStrategy.Sequential;

        public InferenceTier ResolveEntryTier(AgentTask task)
        {
            if (task.Context is not null
                && task.Context.TryGetValue("inferenceTier", out var configuredTier)
                && Enum.TryParse<InferenceTier>(configuredTier, ignoreCase: true, out var explicitTier))
            {
                return explicitTier;
            }

            return task.Type switch
            {
                AgentTaskType.Explore => InferenceTier.Router,
                AgentTaskType.Custom => InferenceTier.Router,
                AgentTaskType.Research => InferenceTier.Processing,
                AgentTaskType.Execute => InferenceTier.Processing,
                AgentTaskType.Analyze => InferenceTier.Analysis,
                _ => InferenceTier.Processing,
            };
        }

        public InferenceTier ResolveTierForAgent(string agentName)
        {
            if (agentName.Contains("Explore", StringComparison.OrdinalIgnoreCase))
            {
                return InferenceTier.Router;
            }

            if (agentName.Contains("Analyze", StringComparison.OrdinalIgnoreCase))
            {
                return InferenceTier.Analysis;
            }

            return InferenceTier.Processing;
        }

        public string ResolveModel(InferenceTier tier) => "model";

        public InferenceTier? GetNextTier(InferenceTier currentTier) => null;
    }

    private sealed class TestAgent(string name) : AppSpecialistAgent
    {
        public string Name { get; } = name;
        public string Description => Name;
        public IReadOnlyList<AgentCapability> Capabilities => [AgentCapability.KnowledgeRetrieval];
        public Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentResult(task.Id, Name, "Completed"));
        public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
