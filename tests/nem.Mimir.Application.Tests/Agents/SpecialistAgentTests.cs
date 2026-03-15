using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Agents.Specialists;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Contracts.Agents;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace nem.Mimir.Application.Tests.Agents;

public sealed class SpecialistAgentTests
{
    private readonly ILlmService _llmService;
    private readonly ISandboxService _sandboxService;

    public SpecialistAgentTests()
    {
        _llmService = Substitute.For<ILlmService>();
        _sandboxService = Substitute.For<ISandboxService>();
    }

    // ──────────────────────── GeneralAgent ────────────────────────

    [Fact]
    public async Task GeneralAgent_CanHandle_ResearchType_ReturnsTrue()
    {
        var agent = CreateGeneralAgent();
        var task = new AgentTask("t1", AgentTaskType.Research, "Tell me about clean architecture");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task GeneralAgent_CanHandle_CustomWithGreeting_ReturnsTrue()
    {
        var agent = CreateGeneralAgent();
        var task = new AgentTask("t2", AgentTaskType.Custom, "Hello, how are you?");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task GeneralAgent_CanHandle_ExecuteType_ReturnsFalse()
    {
        var agent = CreateGeneralAgent();
        var task = new AgentTask("t3", AgentTaskType.Execute, "Run this code");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeFalse();
    }

    [Fact]
    public void GeneralAgent_Capabilities_OnlyKnowledgeRetrieval()
    {
        var agent = CreateGeneralAgent();

        agent.Capabilities.Count.ShouldBe(1);
        agent.Capabilities.ShouldContain(AgentCapability.KnowledgeRetrieval);
    }

    [Fact]
    public void GeneralAgent_TokenBudget_Is4096()
    {
        var agent = CreateGeneralAgent();

        agent.MaxTokenBudget.ShouldBe(4096);
    }

    [Fact]
    public async Task GeneralAgent_Execute_ReturnsCompletedResult()
    {
        var agent = CreateGeneralAgent();
        var task = new AgentTask("t4", AgentTaskType.Research, "Explain CQRS pattern");

        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("CQRS stands for...", "gpt-4", 100, 200, 300, "stop"));

        var result = await agent.ExecuteAsync(task);

        result.TaskId.ShouldBe("t4");
        result.AgentId.ShouldBe("general");
        result.Status.ShouldBe("Completed");
        result.Output.ShouldBe("CQRS stands for...");
        result.TokensUsed.ShouldBe(300);
    }

    [Fact]
    public async Task GeneralAgent_Execute_WhenLlmThrows_ReturnsFailedResult()
    {
        var agent = CreateGeneralAgent();
        var task = new AgentTask("t5", AgentTaskType.Research, "Explain something");

        _llmService.When(x => x.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>()))
            .Do(x => throw new InvalidOperationException("LLM unavailable"));

        var result = await agent.ExecuteAsync(task);

        result.TaskId.ShouldBe("t5");
        result.Status.ShouldBe("Failed");
        result.Output.ShouldNotBeNull();
        result.Output!.ShouldContain("LLM unavailable");
    }

    // ──────────────────────── ExploreAgent ────────────────────────

    [Fact]
    public async Task ExploreAgent_CanHandle_ExploreType_ReturnsTrue()
    {
        var agent = CreateExploreAgent();
        var task = new AgentTask("t6", AgentTaskType.Explore, "Find all controllers");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExploreAgent_CanHandle_CustomWithSearchKeyword_ReturnsTrue()
    {
        var agent = CreateExploreAgent();
        var task = new AgentTask("t7", AgentTaskType.Custom, "Search for the configuration file");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExploreAgent_CanHandle_AnalyzeType_ReturnsFalse()
    {
        var agent = CreateExploreAgent();
        var task = new AgentTask("t8", AgentTaskType.Analyze, "Analyze the performance");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ExploreAgent_Capabilities_IncludesExplorationAndResearch()
    {
        var agent = CreateExploreAgent();

        agent.Capabilities.Count.ShouldBe(3);
        agent.Capabilities.ShouldContain(AgentCapability.CodeExploration);
        agent.Capabilities.ShouldContain(AgentCapability.KnowledgeRetrieval);
        agent.Capabilities.ShouldContain(AgentCapability.WebResearch);
    }

    [Fact]
    public void ExploreAgent_TokenBudget_Is8192()
    {
        var agent = CreateExploreAgent();

        agent.MaxTokenBudget.ShouldBe(8192);
    }

    [Fact]
    public async Task ExploreAgent_Execute_ReturnsCompletedResult()
    {
        var agent = CreateExploreAgent();
        var task = new AgentTask("t9", AgentTaskType.Explore, "Find all services");

        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Found 5 services...", "gpt-4", 150, 250, 400, "stop"));

        var result = await agent.ExecuteAsync(task);

        result.TaskId.ShouldBe("t9");
        result.AgentId.ShouldBe("explore");
        result.Status.ShouldBe("Completed");
        result.Output.ShouldBe("Found 5 services...");
    }

    // ──────────────────────── AnalyzeAgent ────────────────────────

    [Fact]
    public async Task AnalyzeAgent_CanHandle_AnalyzeType_ReturnsTrue()
    {
        var agent = CreateAnalyzeAgent();
        var task = new AgentTask("t10", AgentTaskType.Analyze, "Check this code");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AnalyzeAgent_CanHandle_CustomWithDebugKeyword_ReturnsTrue()
    {
        var agent = CreateAnalyzeAgent();
        var task = new AgentTask("t11", AgentTaskType.Custom, "Debug this null reference issue");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AnalyzeAgent_CanHandle_ExecuteType_ReturnsFalse()
    {
        var agent = CreateAnalyzeAgent();
        var task = new AgentTask("t12", AgentTaskType.Execute, "Run the build");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeFalse();
    }

    [Fact]
    public void AnalyzeAgent_Capabilities_IncludesDeepAnalysis()
    {
        var agent = CreateAnalyzeAgent();

        agent.Capabilities.Count.ShouldBe(3);
        agent.Capabilities.ShouldContain(AgentCapability.DeepAnalysis);
        agent.Capabilities.ShouldContain(AgentCapability.CodeExploration);
        agent.Capabilities.ShouldContain(AgentCapability.KnowledgeRetrieval);
    }

    [Fact]
    public void AnalyzeAgent_TokenBudget_Is16384()
    {
        var agent = CreateAnalyzeAgent();

        agent.MaxTokenBudget.ShouldBe(16384);
    }

    [Fact]
    public async Task AnalyzeAgent_Execute_WithContext_IncludesContextInLlmCall()
    {
        var agent = CreateAnalyzeAgent();
        var context = new Dictionary<string, string>
        {
            ["file"] = "Program.cs",
            ["error"] = "NullReferenceException at line 42",
        };
        var task = new AgentTask("t13", AgentTaskType.Analyze, "Debug this issue", context);

        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Root cause analysis...", "gpt-4", 500, 1000, 1500, "stop"));

        var result = await agent.ExecuteAsync(task);

        result.Status.ShouldBe("Completed");
        result.Output.ShouldBe("Root cause analysis...");

        // Verify LLM was called with messages that include context
        await _llmService.Received(1).SendMessageAsync(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<LlmMessage>>(msgs => msgs.Count == 3), // system + context + user
            Arg.Any<CancellationToken>());
    }

    // ──────────────────────── ExecuteAgent ────────────────────────

    [Fact]
    public async Task ExecuteAgent_CanHandle_ExecuteType_ReturnsTrue()
    {
        var agent = CreateExecuteAgent();
        var task = new AgentTask("t14", AgentTaskType.Execute, "Run the tests");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAgent_CanHandle_CustomWithRunKeyword_ReturnsTrue()
    {
        var agent = CreateExecuteAgent();
        var task = new AgentTask("t15", AgentTaskType.Custom, "Run this script for me");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAgent_CanHandle_ResearchType_ReturnsFalse()
    {
        var agent = CreateExecuteAgent();
        var task = new AgentTask("t16", AgentTaskType.Research, "Tell me about testing");

        var result = await agent.CanHandleAsync(task);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ExecuteAgent_Capabilities_IncludesCodeExecutionAndToolInvocation()
    {
        var agent = CreateExecuteAgent();

        agent.Capabilities.Count.ShouldBe(3);
        agent.Capabilities.ShouldContain(AgentCapability.CodeExecution);
        agent.Capabilities.ShouldContain(AgentCapability.ToolInvocation);
        agent.Capabilities.ShouldContain(AgentCapability.DataProcessing);
    }

    [Fact]
    public void ExecuteAgent_TokenBudget_Is8192()
    {
        var agent = CreateExecuteAgent();

        agent.MaxTokenBudget.ShouldBe(8192);
    }

    [Fact]
    public async Task ExecuteAgent_Execute_WithCodeContext_RunsSandboxAndReturnsResult()
    {
        var agent = CreateExecuteAgent();
        var context = new Dictionary<string, string>
        {
            ["code"] = "print('hello world')",
            ["language"] = "python",
        };
        var task = new AgentTask("t17", AgentTaskType.Execute, "Run this code", context);

        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("I'll execute the Python code.", "gpt-4", 100, 50, 150, "stop"));

        _sandboxService.ExecuteAsync("print('hello world')", "python", Arg.Any<CancellationToken>())
            .Returns(new CodeExecutionResult("hello world\n", "", 0, 120, false));

        var result = await agent.ExecuteAsync(task);

        result.TaskId.ShouldBe("t17");
        result.AgentId.ShouldBe("execute");
        result.Status.ShouldBe("Completed");
        result.Output.ShouldNotBeNull();
        result.Output!.ShouldContain("hello world");
        result.Output.ShouldContain("Exit Code: 0");
        result.Artifacts.ShouldNotBeNull();
        result.Artifacts.ShouldContain(a => a.Contains("sandbox-execution:python"));

        // Verify sandbox was actually called
        await _sandboxService.Received(1).ExecuteAsync("print('hello world')", "python", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAgent_Execute_WithCodeContext_SandboxError_ReturnsCompletedWithErrors()
    {
        var agent = CreateExecuteAgent();
        var context = new Dictionary<string, string>
        {
            ["code"] = "raise Exception('boom')",
            ["language"] = "python",
        };
        var task = new AgentTask("t18", AgentTaskType.Execute, "Run this code", context);

        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Running the code.", "gpt-4", 100, 50, 150, "stop"));

        _sandboxService.ExecuteAsync("raise Exception('boom')", "python", Arg.Any<CancellationToken>())
            .Returns(new CodeExecutionResult("", "Traceback: Exception: boom\n", 1, 80, false));

        var result = await agent.ExecuteAsync(task);

        result.Status.ShouldBe("CompletedWithErrors");
        result.Output.ShouldNotBeNull();
        result.Output!.ShouldContain("Exit Code: 1");
        result.Output.ShouldContain("Stderr:");
    }

    [Fact]
    public async Task ExecuteAgent_Execute_WithoutCodeContext_SkipsSandbox()
    {
        var agent = CreateExecuteAgent();
        var task = new AgentTask("t19", AgentTaskType.Execute, "Automate the deployment");

        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Here's the deployment plan...", "gpt-4", 200, 300, 500, "stop"));

        var result = await agent.ExecuteAsync(task);

        result.Status.ShouldBe("Completed");
        result.Output.ShouldBe("Here's the deployment plan...");

        // Sandbox should NOT have been called
        await _sandboxService.DidNotReceive().ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAgent_ActionLog_RecordsEntries()
    {
        var agent = CreateExecuteAgent();
        var task = new AgentTask("t20", AgentTaskType.Execute, "Run something");

        _llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Done.", "gpt-4", 50, 50, 100, "stop"));

        await agent.ExecuteAsync(task);

        agent.ActionLog.Count.ShouldBeGreaterThan(0);
        agent.ActionLog.ShouldContain(e => e.Action == "TaskStarted");
        agent.ActionLog.ShouldContain(e => e.Action == "LlmResponse");
        agent.ActionLog.ShouldContain(e => e.Action == "TaskCompleted");
    }

    // ──────────────────────── Cross-Agent Routing ────────────────────────

    [Fact]
    public async Task AllAgents_CustomTaskWithNoMatchingKeywords_AllReturnFalse()
    {
        var general = CreateGeneralAgent();
        var explore = CreateExploreAgent();
        var analyze = CreateAnalyzeAgent();
        var execute = CreateExecuteAgent();

        // "abcxyz" matches no keywords in any agent
        var task = new AgentTask("t21", AgentTaskType.Custom, "abcxyz random noise");

        (await general.CanHandleAsync(task)).ShouldBeFalse();
        (await explore.CanHandleAsync(task)).ShouldBeFalse();
        (await analyze.CanHandleAsync(task)).ShouldBeFalse();
        (await execute.CanHandleAsync(task)).ShouldBeFalse();
    }

    [Fact]
    public void AllAgents_HaveUniqueNames()
    {
        var agents = new ISpecialistAgent[]
        {
            CreateGeneralAgent(),
            CreateExploreAgent(),
            CreateAnalyzeAgent(),
            CreateExecuteAgent(),
        };

        var names = agents.Select(a => a.Name).ToList();
        names.Distinct().Count().ShouldBe(names.Count);
    }

    // ──────────────────────── Constructor Validation ────────────────────────

    [Fact]
    public void GeneralAgent_NullLlmService_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new GeneralAgent(null!, Substitute.For<ILogger<GeneralAgent>>()));
    }

    [Fact]
    public void ExecuteAgent_NullSandboxService_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new ExecuteAgent(_llmService, null!, Substitute.For<ILogger<ExecuteAgent>>()));
    }

    // ──────────────────────── Helpers ────────────────────────

    private GeneralAgent CreateGeneralAgent() =>
        new(_llmService, Substitute.For<ILogger<GeneralAgent>>());

    private ExploreAgent CreateExploreAgent() =>
        new(_llmService, Substitute.For<ILogger<ExploreAgent>>());

    private AnalyzeAgent CreateAnalyzeAgent() =>
        new(_llmService, Substitute.For<ILogger<AnalyzeAgent>>());

    private ExecuteAgent CreateExecuteAgent() =>
        new(_llmService, _sandboxService, Substitute.For<ILogger<ExecuteAgent>>());
}
