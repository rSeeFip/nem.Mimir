using nem.Contracts.Agents;
using nem.Contracts.Inference;
using nem.Mimir.Application.Agents;
using nem.Mimir.Application.Common.Interfaces;
using Shouldly;

namespace nem.Mimir.Application.Tests.Agents;

public sealed class DefaultOrchestrationPlanProviderTests
{
    [Fact]
    public void ResolvePlan_ShouldPreserveDefaultStrategyMaxTurnsAndMappings()
    {
        var provider = new DefaultOrchestrationPlanProvider();
        var context = new AgentExecutionContext(new AgentTask("t1", AgentTaskType.Explore, "inspect"));

        var plan = provider.ResolvePlan(context);

        plan.DefaultMaxTurns.ShouldBe(10);
        plan.EscalationThreshold.ShouldBe(0.7d);
        plan.ResolveStrategy(new AgentTask("t2", AgentTaskType.Custom, "prompt")).ShouldBe(AgentCoordinationStrategy.Sequential);
        plan.ResolveStrategy(new AgentTask("t3", AgentTaskType.Custom, "prompt", new Dictionary<string, string> { ["strategy"] = "parallel" }))
            .ShouldBe(AgentCoordinationStrategy.Parallel);
        plan.ResolveEntryTier(new AgentTask("t4", AgentTaskType.Analyze, "prompt")).ShouldBe(InferenceTier.Analysis);
        plan.ResolveEntryTier(new AgentTask("t5", AgentTaskType.Explore, "prompt", new Dictionary<string, string> { ["inferenceTier"] = "Processing" }))
            .ShouldBe(InferenceTier.Processing);
    }

    [Theory]
    [InlineData("Explore Agent", InferenceTier.Router)]
    [InlineData("Schema Validator", InferenceTier.Validation)]
    [InlineData("Analyze Agent", InferenceTier.Analysis)]
    [InlineData("Human Escalation", InferenceTier.Escalation)]
    [InlineData("Execute Agent", InferenceTier.Processing)]
    public void ResolvePlan_ShouldPreserveDefaultTierAndModelRules(string agentName, InferenceTier expectedTier)
    {
        var provider = new DefaultOrchestrationPlanProvider();
        var plan = provider.ResolvePlan(new AgentExecutionContext(new AgentTask("t6", AgentTaskType.Custom, "prompt")));

        plan.ResolveTierForAgent(agentName).ShouldBe(expectedTier);
    }

    [Fact]
    public void ResolvePlan_ShouldPreserveDefaultModelAndEscalationPath()
    {
        var provider = new DefaultOrchestrationPlanProvider();
        var plan = provider.ResolvePlan(new AgentExecutionContext(new AgentTask("t7", AgentTaskType.Custom, "prompt")));

        plan.ResolveModel(InferenceTier.Router).ShouldBe("phi-4-mini");
        plan.ResolveModel(InferenceTier.Validation).ShouldBe("phi-4-mini");
        plan.ResolveModel(InferenceTier.Processing).ShouldBe("gpt-4o-mini");
        plan.ResolveModel(InferenceTier.Analysis).ShouldBe("gpt-4o");
        plan.ResolveModel(InferenceTier.Escalation).ShouldBe("human-review");
        plan.GetNextTier(InferenceTier.Router).ShouldBe(InferenceTier.Validation);
        plan.GetNextTier(InferenceTier.Validation).ShouldBe(InferenceTier.Processing);
        plan.GetNextTier(InferenceTier.Processing).ShouldBe(InferenceTier.Analysis);
        plan.GetNextTier(InferenceTier.Analysis).ShouldBe(InferenceTier.Escalation);
        plan.GetNextTier(InferenceTier.Escalation).ShouldBeNull();
    }
}
