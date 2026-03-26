using NSubstitute;
using Shouldly;
using nem.Contracts.Agents;
using nem.Mimir.McpServer.Tools;

namespace nem.Mimir.McpServer.Tests.Tools;

public sealed class MimirGetAgentsToolTests
{
    private readonly IAgentOrchestrator _orchestrator = Substitute.For<IAgentOrchestrator>();

    [Fact]
    public async Task GetAgentsAsync_ReturnsSerializedAgentList()
    {
        var agent = Substitute.For<ISpecialistAgent>();
        agent.Name.Returns("AnalyzeAgent");
        agent.Description.Returns("Performs deep analysis");
        agent.Capabilities.Returns(new List<AgentCapability> { AgentCapability.DeepAnalysis });

        _orchestrator.ListAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ISpecialistAgent> { agent });

        var result = await MimirGetAgentsTool.GetAgentsAsync(_orchestrator, CancellationToken.None);

        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("AnalyzeAgent");
        result.ShouldContain("DeepAnalysis");
    }

    [Fact]
    public async Task GetAgentsAsync_EmptyList_ReturnsEmptyArray()
    {
        _orchestrator.ListAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ISpecialistAgent>());

        var result = await MimirGetAgentsTool.GetAgentsAsync(_orchestrator, CancellationToken.None);

        result.ShouldBe("[]");
    }
}
