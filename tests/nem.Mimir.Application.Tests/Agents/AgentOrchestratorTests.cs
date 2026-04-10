using nem.Mimir.Application.Agents;
using nem.Mimir.Application.Agents.Selection;
using nem.Mimir.Application.Agents.Services;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using NSubstitute;
using NSubstitute.Core;
using Shouldly;
using nem.Contracts.Agents;
using nem.Contracts.Inference;
using ContractSpecialistAgent = nem.Contracts.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Tests.Agents;

public sealed class AgentOrchestratorTests
{
    private readonly ILlmService _llmService = Substitute.For<ILlmService>();
    private readonly IOrchestrationPlanProvider _defaultPlanProvider = new DefaultOrchestrationPlanProvider();

    [Fact]
    public async Task Dispatcher_ShouldRouteByCapabilityMatch()
    {
        var general = new TestAgent(
            "general",
            [AgentCapability.KnowledgeRetrieval],
            static _ => true,
            static task => new AgentResult(task.Id, "general", "Completed", "general"));

        var execute = new TestAgent(
            "execute",
            [AgentCapability.CodeExecution],
            static _ => true,
            static task => new AgentResult(task.Id, "execute", "Completed", "execute"));

        var dispatcher = new AgentDispatcher([general, execute]);
        var selected = await dispatcher.DispatchAsync(new AgentTask("t1", AgentTaskType.Execute, "run the build and tests"));

        selected.Name.ShouldBe("execute");
    }

    [Fact]
    public async Task Dispatcher_ShouldFallbackToGeneralAgent_WhenNoSpecialistMatches()
    {
        var general = new TestAgent(
            "general",
            [AgentCapability.KnowledgeRetrieval],
            static _ => true,
            static task => new AgentResult(task.Id, "general", "Completed", "general"));

        var specialized = new TestAgent(
            "analyze",
            [AgentCapability.DeepAnalysis],
            static _ => false,
            static task => new AgentResult(task.Id, "analyze", "Completed", "analyze"));

        var dispatcher = new AgentDispatcher([specialized, general]);
        var selected = await dispatcher.DispatchAsync(new AgentTask("t2", AgentTaskType.Custom, "nonsense prompt"));

        selected.Name.ShouldBe("general");
    }

    [Fact]
    public async Task Coordinator_Sequential_ShouldRunAgentsInOrder()
    {
        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("ok", "phi-4-mini", 1, 1, 2, "stop"));

        var coordinator = new AgentCoordinator(_llmService);
        var order = new List<string>();

        var first = new TestAgent(
            "first",
            [AgentCapability.CodeExploration],
            static _ => true,
            task =>
            {
                order.Add("first");
                return new AgentResult(task.Id, "first", "Completed", "one", 1, 10);
            });

        var second = new TestAgent(
            "second",
            [AgentCapability.DeepAnalysis],
            static _ => true,
            task =>
            {
                order.Add("second");
                return new AgentResult(task.Id, "second", "Completed", "two", 1, 10);
            });

        var context = new AgentExecutionContext(new AgentTask("t3", AgentTaskType.Explore, "inspect"));
        var result = await coordinator.ExecuteAsync(context, [first, second], AgentCoordinationStrategy.Sequential);

        result.Status.ShouldBe("Completed");
        result.AgentId.ShouldBe("second");
        order.ShouldBe(["first", "second"]);
        context.ToolResults.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Coordinator_Parallel_ShouldMergeResults()
    {
        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("ok", "phi-4-mini", 1, 1, 2, "stop"));

        var coordinator = new AgentCoordinator(_llmService);
        var first = new TestAgent(
            "first",
            [AgentCapability.CodeExploration],
            static _ => true,
            static task => new AgentResult(task.Id, "first", "Completed", "alpha", 2, 40, ["a1"]));

        var second = new TestAgent(
            "second",
            [AgentCapability.DeepAnalysis],
            static _ => true,
            static task => new AgentResult(task.Id, "second", "Completed", "beta", 3, 60, ["b1"]));

        var context = new AgentExecutionContext(new AgentTask("t4", AgentTaskType.Analyze, "analyze this"));
        var result = await coordinator.ExecuteAsync(context, [first, second], AgentCoordinationStrategy.Parallel);

        result.Status.ShouldBe("Completed");
        result.AgentId.ShouldBe("parallel-coordinator");
        result.Output.ShouldContain("alpha");
        result.Output.ShouldContain("beta");
        result.TokensUsed.ShouldBe(5);
        result.Artifacts.ShouldNotBeNull();
        result.Artifacts.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Coordinator_ShouldEnforceTurnLimit()
    {
        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("ok", "phi-4-mini", 1, 1, 2, "stop"));

        var coordinator = new AgentCoordinator(_llmService);
        var agent = new TestAgent(
            "agent",
            [AgentCapability.CodeExploration],
            static _ => true,
            static task => new AgentResult(task.Id, "agent", "Completed", "ok"));

        var context = new AgentExecutionContext(new AgentTask("t5", AgentTaskType.Explore, "explore"), maxTurns: 1);

        await Should.ThrowAsync<InvalidOperationException>(
            () => coordinator.ExecuteAsync(context, [agent, agent], AgentCoordinationStrategy.Sequential));
    }

    [Fact]
    public async Task Coordinator_ShouldReturnTimedOut_WhenAgentExceedsTurnTimeout()
    {
        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("ok", "phi-4-mini", 1, 1, 2, "stop"));

        var coordinator = new AgentCoordinator(_llmService);
        var slowAgent = new SlowAgent("slow");
        var context = new AgentExecutionContext(new AgentTask("t6", AgentTaskType.Execute, "run slowly"));

        var result = await coordinator.ExecuteAsync(context, [slowAgent], AgentCoordinationStrategy.Sequential);

        result.Status.ShouldBe("TimedOut");
        result.AgentId.ShouldBe("slow");
    }

    [Fact]
    public async Task Orchestrator_ShouldDispatchAndTrackTaskStatus()
    {
        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("ok", "phi-4-mini", 1, 1, 2, "stop"));

        var agent = new TestAgent(
            "general",
            [AgentCapability.KnowledgeRetrieval],
            static _ => true,
            static task => new AgentResult(task.Id, "general", "Completed", "done"));

        var dispatcher = new AgentDispatcher([agent]);
        var coordinator = new AgentCoordinator(_llmService);
        var trajectoryRecorder = Substitute.For<ITrajectoryRecorder>();
        trajectoryRecorder
            .StartRecordingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(global::nem.Contracts.Identity.TrajectoryId.New());
        var orchestrator = new AgentOrchestrator(dispatcher, coordinator, _llmService, trajectoryRecorder, _defaultPlanProvider);

        var task = new AgentTask("t7", AgentTaskType.Research, "help me", new Dictionary<string, string> { ["strategy"] = "sequential" });
        var result = await orchestrator.DispatchAsync(task);
        var status = await orchestrator.GetTaskStatusAsync(task.Id);

        result.Status.ShouldBe("Completed");
        status.ShouldNotBeNull();
        status!.Status.ShouldBe("Completed");
    }

    [Fact]
    public async Task Orchestrator_Tiered_ShouldEscalateAcrossTiers_OnLowConfidence()
    {
        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("ok", "phi-4-mini", 1, 1, 2, "stop"));

        var router = new TestAgent(
            "Explore Agent",
            [AgentCapability.CodeExploration],
            static _ => true,
            static task => new AgentResult(task.Id, "explore", "Completed", "confidence=0.40"));

        var validator = new TestAgent(
            "Schema Validator",
            [AgentCapability.KnowledgeRetrieval],
            static _ => true,
            static task => new AgentResult(task.Id, "validator", "Completed", "confidence=0.55"));

        var processor = new TestAgent(
            "Execute Agent",
            [AgentCapability.CodeExecution],
            static _ => true,
            static task => new AgentResult(task.Id, "execute", "Completed", "confidence=0.62"));

        var analyzer = new TestAgent(
            "Analyze Agent",
            [AgentCapability.DeepAnalysis],
            static _ => true,
            static task => new AgentResult(task.Id, "analyze", "Completed", "confidence=0.88"));

        var dispatcher = new AgentDispatcher([router, validator, processor, analyzer]);
        var coordinator = new AgentCoordinator(_llmService);
        var recorder = Substitute.For<ITrajectoryRecorder>();
        recorder.StartRecordingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(global::nem.Contracts.Identity.TrajectoryId.New());
        var orchestrator = new AgentOrchestrator(dispatcher, coordinator, _llmService, recorder, _defaultPlanProvider);

        var task = new AgentTask(
            "tiered-1",
            AgentTaskType.Explore,
            "triage this issue",
            new Dictionary<string, string> { ["strategy"] = "tiered" });

        var result = await orchestrator.DispatchAsync(task);

        result.Status.ShouldBe("Completed");
        result.AgentId.ShouldBe("analyze");
    }

    [Fact]
    public async Task Orchestrator_Tiered_ShouldUsePlanDataForTierAndModelSelection()
    {
        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("ok", "phi-4-mini", 1, 1, 2, "stop"));

        IReadOnlyDictionary<string, string>? seenContext = null;
        var analyzer = new TestAgent(
            "Analyze Agent",
            [AgentCapability.DeepAnalysis],
            static _ => true,
            task =>
            {
                seenContext = task.Context;
                return new AgentResult(task.Id, "analyze", "Completed", "confidence=0.90");
            });

        var plan = new ProcessOrchestrationPlan(
            defaultMaxTurns: 10,
            selectionProcess: SelectionProcessDefinition.Default,
            tierConfiguration: new TierConfiguration(
                modelByTier: new Dictionary<InferenceTier, string>
                {
                    [InferenceTier.Router] = "router-model",
                    [InferenceTier.Validation] = "validation-model",
                    [InferenceTier.Processing] = "processing-model",
                    [InferenceTier.Analysis] = "analysis-model-x",
                    [InferenceTier.Escalation] = "human-review",
                },
                agentTierAssignments: new Dictionary<string, InferenceTier>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Analyze Agent"] = InferenceTier.Analysis,
                },
                entryTierByTaskType: new Dictionary<AgentTaskType, InferenceTier>
                {
                    [AgentTaskType.Explore] = InferenceTier.Analysis,
                },
                escalationPath:
                [
                    InferenceTier.Analysis,
                    InferenceTier.Escalation,
                ],
                escalationThreshold: 0.5d));
        var planProvider = Substitute.For<IOrchestrationPlanProvider>();
        planProvider.ResolvePlan(Arg.Any<AgentExecutionContext>()).Returns(plan);

        var dispatcher = new AgentDispatcher([analyzer]);
        var coordinator = new AgentCoordinator(_llmService);
        var recorder = Substitute.For<ITrajectoryRecorder>();
        recorder.StartRecordingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(global::nem.Contracts.Identity.TrajectoryId.New());
        var orchestrator = new AgentOrchestrator(
            dispatcher,
            coordinator,
            _llmService,
            recorder,
            planProvider);

        var task = new AgentTask(
            "tiered-2",
            AgentTaskType.Explore,
            "run deployment checks",
            new Dictionary<string, string> { ["strategy"] = "tiered" });

        var result = await orchestrator.DispatchAsync(task);

        result.Status.ShouldBe("Completed");
        seenContext.ShouldNotBeNull();
        seenContext!["inferenceTier"].ShouldBe("Analysis");
        seenContext["inferenceModel"].ShouldBe("analysis-model-x");
        planProvider.Received(1).ResolvePlan(Arg.Any<AgentExecutionContext>());
    }

    [Fact]
    public async Task Orchestrator_ShouldUsePlanDefaultMaxTurns_WhenContextOverrideMissing()
    {
        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("ok", "phi-4-mini", 1, 1, 2, "stop"));

        var plan = Substitute.For<IOrchestrationPlan>();
        plan.DefaultMaxTurns.Returns(2);
        plan.SelectionProcess.Returns(SelectionProcessDefinition.Default);
        plan.TierConfiguration.Returns(TierConfiguration.Default);
        plan.EscalationThreshold.Returns(0.7d);
        plan.ResolveStrategy(Arg.Any<AgentTask>()).Returns(AgentCoordinationStrategy.Sequential);
        var planProvider = Substitute.For<IOrchestrationPlanProvider>();
        planProvider.ResolvePlan(Arg.Any<AgentExecutionContext>()).Returns(plan);

        var dispatcher = new AgentDispatcher([]);
        var coordinator = new AgentCoordinator(_llmService);
        var recorder = Substitute.For<ITrajectoryRecorder>();
        recorder.StartRecordingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(global::nem.Contracts.Identity.TrajectoryId.New());
        var orchestrator = new AgentOrchestrator(dispatcher, coordinator, _llmService, recorder, planProvider);

        var status = await orchestrator.DispatchAsync(new AgentTask("no-agents", AgentTaskType.Custom, "prompt"));

        status.Status.ShouldBe("Failed");
        plan.Received(1).ResolveStrategy(Arg.Any<AgentTask>());
        _ = plan.Received(1).DefaultMaxTurns;
    }

    private sealed class TestAgent : ContractSpecialistAgent
    {
        private readonly Func<AgentTask, bool> _canHandle;
        private readonly Func<AgentTask, AgentResult> _execute;

        public TestAgent(
            string name,
            IReadOnlyList<AgentCapability> capabilities,
            Func<AgentTask, bool> canHandle,
            Func<AgentTask, AgentResult> execute)
        {
            Name = name;
            Description = $"{name} agent";
            Capabilities = capabilities;
            _canHandle = canHandle;
            _execute = execute;
        }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyList<AgentCapability> Capabilities { get; }

        public Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_execute(task));
        }

        public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_canHandle(task));
        }
    }

    private sealed class SlowAgent : ContractSpecialistAgent
    {
        public SlowAgent(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string Description => "slow agent";

        public IReadOnlyList<AgentCapability> Capabilities => [AgentCapability.CodeExecution];

        public async Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(35), cancellationToken);
            return new AgentResult(task.Id, Name, "Completed", "late");
        }

        public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}

internal static class TestCompatibilityExtensions
{
    public static ConfiguredCall ThrowsAsync<T>(this Task<T> task, Exception exception)
    {
        return task.Returns(Task.FromException<T>(exception));
    }

    public static void ShouldContain(this string? actual, string expected, Case caseSensitivity = Case.Insensitive, string? customMessage = null)
    {
        actual.ShouldNotBeNull();
        Shouldly.ShouldBeStringTestExtensions.ShouldContain(actual, expected, caseSensitivity, customMessage);
    }
}
