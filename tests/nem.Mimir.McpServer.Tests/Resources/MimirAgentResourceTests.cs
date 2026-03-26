using NSubstitute;
using Shouldly;
using nem.Contracts.Agents;
using nem.Mimir.McpServer.Resources;

namespace nem.Mimir.McpServer.Tests.Resources;

public sealed class MimirAgentResourceTests
{
    private readonly IAgentOrchestrator _orchestrator = Substitute.For<IAgentOrchestrator>();

    [Fact]
    public async Task GetAgentAsync_ExistingAgent_ReturnsDetails()
    {
        var agent = Substitute.For<ISpecialistAgent>();
        agent.Name.Returns("ExploreAgent");
        agent.Description.Returns("Explores code structures");
        agent.Capabilities.Returns(new List<AgentCapability> { AgentCapability.CodeExploration });

        _orchestrator.GetAgentAsync("ExploreAgent", Arg.Any<CancellationToken>())
            .Returns(agent);

        var result = await MimirAgentResource.GetAgentAsync(
            "ExploreAgent", _orchestrator, CancellationToken.None);

        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("ExploreAgent");
        result.ShouldContain("CodeExploration");
    }

    [Fact]
    public async Task GetAgentAsync_NonExistentAgent_ReturnsError()
    {
        _orchestrator.GetAgentAsync("Unknown", Arg.Any<CancellationToken>())
            .Returns((ISpecialistAgent?)null);

        var result = await MimirAgentResource.GetAgentAsync(
            "Unknown", _orchestrator, CancellationToken.None);

        result.ShouldContain("error");
        result.ShouldContain("Unknown");
    }
}
