using NSubstitute;
using Shouldly;
using nem.Contracts.Agents;
using nem.Mimir.McpServer.Tools;

namespace nem.Mimir.McpServer.Tests.Tools;

public sealed class MimirDelegateTaskToolTests
{
    private readonly IAgentOrchestrator _orchestrator = Substitute.For<IAgentOrchestrator>();

    [Fact]
    public async Task DelegateTaskAsync_ValidTaskType_DispatchesCorrectly()
    {
        var expected = new AgentResult("task-1", "AnalyzeAgent", "Completed", "Analysis done", 100, 500);

        _orchestrator.DispatchAsync(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await MimirDelegateTaskTool.DelegateTaskAsync(
            "Analyze", "Analyze this code", 1, 60, _orchestrator, CancellationToken.None);

        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("Completed");
        result.ShouldContain("Analysis done");

        await _orchestrator.Received(1)
            .DispatchAsync(
                Arg.Is<AgentTask>(t =>
                    t.Type == AgentTaskType.Analyze &&
                    t.Prompt == "Analyze this code" &&
                    t.Priority == 1 &&
                    t.TimeoutSeconds == 60),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DelegateTaskAsync_InvalidTaskType_FallsBackToCustom()
    {
        var expected = new AgentResult("task-2", "GeneralAgent", "Completed", "Done");

        _orchestrator.DispatchAsync(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        await MimirDelegateTaskTool.DelegateTaskAsync(
            "InvalidType", "Do something", 0, 300, _orchestrator, CancellationToken.None);

        await _orchestrator.Received(1)
            .DispatchAsync(
                Arg.Is<AgentTask>(t => t.Type == AgentTaskType.Custom),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DelegateTaskAsync_ZeroTimeout_DefaultsTo300()
    {
        var expected = new AgentResult("task-3", "ExploreAgent", "Completed");

        _orchestrator.DispatchAsync(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        await MimirDelegateTaskTool.DelegateTaskAsync(
            "Explore", "Explore repo", 0, 0, _orchestrator, CancellationToken.None);

        await _orchestrator.Received(1)
            .DispatchAsync(
                Arg.Is<AgentTask>(t => t.TimeoutSeconds == 300),
                Arg.Any<CancellationToken>());
    }
}
