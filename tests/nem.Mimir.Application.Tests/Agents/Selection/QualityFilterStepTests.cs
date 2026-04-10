using Microsoft.Extensions.Logging;
using nem.Contracts.Agents;
using nem.Mimir.Application.Agents.Selection;
using nem.Mimir.Application.Agents.Selection.Steps;
using NSubstitute;
using Shouldly;
using AppSpecialistAgent = nem.Mimir.Application.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Tests.Agents.Selection;

public sealed class QualityFilterStepTests
{
    private readonly ISkillQualityProvider _qualityProvider = Substitute.For<ISkillQualityProvider>();
    private readonly ILogger<QualityFilterStep> _logger = Substitute.For<ILogger<QualityFilterStep>>();

    [Fact]
    public async Task ExecuteAsync_FiltersOutBelowThreshold_WhenAlternativesExist()
    {
        var degraded = CreateScoredAgent("degraded", "low quality", AgentCapability.DeepAnalysis);
        var healthy = CreateScoredAgent("healthy", "high quality", AgentCapability.DeepAnalysis);

        _qualityProvider.GetQualityAsync("degraded", Arg.Any<CancellationToken>())
            .Returns(new SkillQualityInfo(0.2, 100, TimeSpan.FromMilliseconds(900)));
        _qualityProvider.GetQualityAsync("healthy", Arg.Any<CancellationToken>())
            .Returns(new SkillQualityInfo(0.9, 100, TimeSpan.FromMilliseconds(200)));

        var sut = new QualityFilterStep(_qualityProvider, _logger);
        var context = CreateContext("analyze architecture", degraded, healthy);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.Candidates.Count.ShouldBe(1);
        result.Candidates[0].Agent.Name.ShouldBe("healthy");
        result.Candidates[0].StepScores["quality"].ShouldBe(0.9);
    }

    [Fact]
    public async Task ExecuteAsync_KeepsAll_WhenAllAgentsAreDegraded()
    {
        var first = CreateScoredAgent("first", "first", AgentCapability.DeepAnalysis);
        var second = CreateScoredAgent("second", "second", AgentCapability.DeepAnalysis);

        _qualityProvider.GetQualityAsync("first", Arg.Any<CancellationToken>())
            .Returns(new SkillQualityInfo(0.2, 40, TimeSpan.FromMilliseconds(800)));
        _qualityProvider.GetQualityAsync("second", Arg.Any<CancellationToken>())
            .Returns(new SkillQualityInfo(0.3, 40, TimeSpan.FromMilliseconds(750)));

        var sut = new QualityFilterStep(_qualityProvider, _logger);
        var context = CreateContext("analyze issues", first, second);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.Candidates.Count.ShouldBe(2);
        result.Candidates.Any(x => x.Agent.Name == "first").ShouldBeTrue();
        result.Candidates.Any(x => x.Agent.Name == "second").ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PassesThroughAgentsWithNoQualityData_AsGood()
    {
        var noData = CreateScoredAgent("no-data", "unknown quality", AgentCapability.DeepAnalysis);
        var degraded = CreateScoredAgent("degraded", "low quality", AgentCapability.DeepAnalysis);

        _qualityProvider.GetQualityAsync("no-data", Arg.Any<CancellationToken>())
            .Returns((SkillQualityInfo?)null);
        _qualityProvider.GetQualityAsync("degraded", Arg.Any<CancellationToken>())
            .Returns(new SkillQualityInfo(0.1, 120, TimeSpan.FromMilliseconds(1000)));

        var sut = new QualityFilterStep(_qualityProvider, _logger);
        var context = CreateContext("analyze performance", noData, degraded);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.Candidates.Count.ShouldBe(1);
        result.Candidates[0].Agent.Name.ShouldBe("no-data");
        result.Candidates[0].StepScores["quality"].ShouldBe(1.0);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsFiltering_WhenProviderReturnsNullForAllAgents()
    {
        var first = CreateScoredAgent("first", "first", AgentCapability.DeepAnalysis);
        var second = CreateScoredAgent("second", "second", AgentCapability.KnowledgeRetrieval);

        _qualityProvider.GetQualityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SkillQualityInfo?)null);

        var sut = new QualityFilterStep(_qualityProvider, _logger);
        var context = CreateContext("find docs", first, second);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.Candidates.Count.ShouldBe(2);
        result.Candidates.Select(x => x.Agent.Name).ShouldBe(["first", "second"]);
        result.Candidates.All(x => x.StepScores.ContainsKey("quality")).ShouldBeTrue();
    }

    private static SelectionContext CreateContext(string prompt, params ScoredAgent[] candidates)
    {
        var task = new AgentTask("t-quality", AgentTaskType.Analyze, prompt);
        return new SelectionContext(task, candidates, SelectionProcessDefinition.Default);
    }

    private static ScoredAgent CreateScoredAgent(string name, string description, params AgentCapability[] capabilities)
    {
        return ScoredAgent.Create(new TestAgent(name, description, capabilities));
    }

    private sealed class TestAgent(string name, string description, IReadOnlyList<AgentCapability> capabilities) : AppSpecialistAgent
    {
        public string Name { get; } = name;
        public string Description { get; } = description;
        public IReadOnlyList<AgentCapability> Capabilities { get; } = capabilities;

        public Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentResult(task.Id, Name, "Completed"));
        }

        public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
