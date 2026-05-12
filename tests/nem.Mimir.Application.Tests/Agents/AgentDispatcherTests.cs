using nem.Contracts.Agents;
using nem.Mimir.Application.Agents;
using nem.Mimir.Application.Agents.Selection;
using nem.Mimir.Application.Common.Interfaces;
using Shouldly;
using AppSpecialistAgent = nem.Mimir.Application.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Tests.Agents;

public sealed class AgentDispatcherTests
{
    [Fact]
    public async Task GetCandidatesAsync_RunsSelectionStepsInOrder_AfterCanHandlePrefilter()
    {
        var first = new TestAgent("first", canHandle: true);
        var second = new TestAgent("second", canHandle: false);

        var callOrder = new List<string>();
        var steps = new ISelectionStep[]
        {
            new RecordingStep("quality", callOrder),
            new RecordingStep("bm25", callOrder),
            new RecordingStep("embedding", callOrder),
        };

        var sut = new AgentDispatcher([first, second], steps);

        var result = await sut.GetCandidatesAsync(new AgentTask("t1", AgentTaskType.Custom, "prompt"));

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("first");
        callOrder.ShouldBe(["quality", "bm25", "embedding"]);
    }

    [Fact]
    public async Task GetCandidatesAsync_RanksByCompositePipelineScore()
    {
        var alpha = new TestAgent("alpha", canHandle: true);
        var beta = new TestAgent("beta", canHandle: true);
        var gamma = new TestAgent("gamma", canHandle: true);

        var step1 = new ScoreStep(new Dictionary<string, double>
        {
            ["alpha"] = 0.10,
            ["beta"] = 0.90,
            ["gamma"] = 0.30,
        }, "quality");

        var step2 = new ScoreStep(new Dictionary<string, double>
        {
            ["alpha"] = 0.80,
            ["beta"] = 0.05,
            ["gamma"] = 0.20,
        }, "bm25");

        var step3 = new ScoreStep(new Dictionary<string, double>
        {
            ["alpha"] = 0.20,
            ["beta"] = 0.10,
            ["gamma"] = 0.70,
        }, "embedding");

        var sut = new AgentDispatcher([alpha, beta, gamma], [step1, step2, step3]);

        var result = await sut.GetCandidatesAsync(new AgentTask("t2", AgentTaskType.Custom, "prompt"));

        result.Select(x => x.Name).ShouldBe(["gamma", "alpha", "beta"]);
    }

    [Fact]
    public async Task GetCandidatesAsync_WhenProcessDefinitionOverridesStepSequence_UsesConfiguredPipeline()
    {
        var alpha = new TestAgent("alpha", canHandle: true);
        var beta = new TestAgent("beta", canHandle: true);
        var gamma = new TestAgent("gamma", canHandle: true);

        var steps = new ISelectionStep[]
        {
            new ScoreStep(new Dictionary<string, double>
            {
                ["alpha"] = 0.10,
                ["beta"] = 0.90,
                ["gamma"] = 0.30,
            }, SelectionStepNames.Quality),
            new ScoreStep(new Dictionary<string, double>
            {
                ["alpha"] = 0.80,
                ["beta"] = 0.05,
                ["gamma"] = 0.20,
            }, SelectionStepNames.Bm25),
            new ScoreStep(new Dictionary<string, double>
            {
                ["alpha"] = 0.20,
                ["beta"] = 0.10,
                ["gamma"] = 0.70,
            }, SelectionStepNames.Embedding),
        };

        var plan = new ProcessOrchestrationPlan(
            defaultMaxTurns: 10,
            selectionProcess: new SelectionProcessDefinition([SelectionStepDefinition.Quality()]),
            tierConfiguration: TierConfiguration.Default);

        var sut = new AgentDispatcher([alpha, beta, gamma], steps, new StubPlanProvider(plan));

        var result = await sut.GetCandidatesAsync(new AgentTask("t2-override", AgentTaskType.Custom, "prompt"));

        result.Select(x => x.Name).ShouldBe(["beta", "gamma", "alpha"]);
    }

    [Fact]
    public async Task GetCandidatesAsync_WhenNoAgentCanHandle_FallsBackToGeneral()
    {
        var analyze = new TestAgent("analyze", canHandle: false);
        var general = new TestAgent("general", canHandle: false);

        var sut = new AgentDispatcher([analyze, general], [new RecordingStep("quality", [])]);

        var result = await sut.GetCandidatesAsync(new AgentTask("t3", AgentTaskType.Custom, "prompt"));

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("general");
    }

    [Fact]
    public async Task GetCandidatesAsync_WhenNoAgentsRegistered_ReturnsEmpty()
    {
        var sut = new AgentDispatcher([], [new RecordingStep("quality", [])]);

        var result = await sut.GetCandidatesAsync(new AgentTask("t4", AgentTaskType.Custom, "prompt"));

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCandidatesAsync_WhenFallbackDefinitionOverridesPreferredPattern_UsesConfiguredFallback()
    {
        var analyze = new TestAgent("analyze", canHandle: false);
        var general = new TestAgent("general", canHandle: false);

        var plan = new ProcessOrchestrationPlan(
            defaultMaxTurns: 10,
            selectionProcess: new SelectionProcessDefinition(
                [SelectionStepDefinition.Quality()],
                new SelectionFallbackDefinition(["analyze"], UseAlphabeticalFallback: false)),
            tierConfiguration: TierConfiguration.Default);

        var sut = new AgentDispatcher([general, analyze], [new RecordingStep(SelectionStepNames.Quality, [])], new StubPlanProvider(plan));

        var result = await sut.GetCandidatesAsync(new AgentTask("t5", AgentTaskType.Custom, "prompt"));

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("analyze");
    }

    private sealed class RecordingStep(string name, List<string> callOrder) : ISelectionStep
    {
        public string Name => name;

        public Task<SelectionContext> ExecuteAsync(SelectionContext context, CancellationToken ct)
        {
            callOrder.Add(name);
            return Task.FromResult(context);
        }
    }

    private sealed class ScoreStep(IReadOnlyDictionary<string, double> scoresByAgent, string stepName) : ISelectionStep
    {
        public string Name => stepName;

        public Task<SelectionContext> ExecuteAsync(SelectionContext context, CancellationToken ct)
        {
            var updated = context.Candidates
                .Select(candidate =>
                {
                    var score = scoresByAgent.TryGetValue(candidate.Agent.Name, out var value) ? value : 0d;
                    return candidate.AddStepScore(stepName, score);
                })
                .ToList();

            return Task.FromResult(context.WithCandidates(updated));
        }
    }

    private sealed class StubPlanProvider(IOrchestrationPlan plan) : IOrchestrationPlanProvider
    {
        public IOrchestrationPlan ResolvePlan(AgentExecutionContext context) => plan;
    }

    private sealed class TestAgent(string name, bool canHandle) : AppSpecialistAgent
    {
        public string Name { get; } = name;
        public string Description => $"{Name} agent";
        public IReadOnlyList<AgentCapability> Capabilities => [AgentCapability.KnowledgeRetrieval];

        public Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentResult(task.Id, Name, "Completed"));

        public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
            => Task.FromResult(canHandle);
    }
}
