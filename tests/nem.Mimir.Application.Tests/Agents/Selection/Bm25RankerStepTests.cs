using nem.Contracts.Agents;
using nem.Mimir.Application.Agents.Selection;
using nem.Mimir.Application.Agents.Selection.Steps;
using Shouldly;
using AppSpecialistAgent = nem.Mimir.Application.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Tests.Agents.Selection;

public sealed class Bm25RankerStepTests
{
    [Fact]
    public async Task ExecuteAsync_RanksMatchingAgentHigher()
    {
        var codeAgent = CreateScoredAgent(
            "code-explorer",
            "Explores repositories and refactors methods",
            AgentCapability.CodeExploration);

        var researchAgent = CreateScoredAgent(
            "researcher",
            "Finds public documentation and external references",
            AgentCapability.KnowledgeRetrieval);

        var sut = new Bm25RankerStep();
        var context = CreateContext("refactor repository class methods", codeAgent, researchAgent);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.Candidates.Count.ShouldBe(2);
        result.Candidates[0].Agent.Name.ShouldBe("code-explorer");
        result.Candidates[0].StepScores["bm25"].ShouldBeGreaterThan(result.Candidates[1].StepScores["bm25"]);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesEmptyPrompt_Gracefully()
    {
        var first = CreateScoredAgent("first", "first desc", AgentCapability.DeepAnalysis);
        var second = CreateScoredAgent("second", "second desc", AgentCapability.ToolInvocation);

        var sut = new Bm25RankerStep();
        var context = CreateContext(string.Empty, first, second);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.Candidates.Count.ShouldBe(2);
        result.Candidates.All(x => x.StepScores["bm25"] == 0).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NoKeywordMatch_GetsZeroScore()
    {
        var unrelated = CreateScoredAgent(
            "web-agent",
            "Performs internet search and online retrieval",
            AgentCapability.WebResearch);

        var sut = new Bm25RankerStep();
        var context = CreateContext("quantum compiler optimization", unrelated);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.Candidates.Count.ShouldBe(1);
        result.Candidates[0].StepScores["bm25"].ShouldBe(0d);
    }

    private static SelectionContext CreateContext(string prompt, params ScoredAgent[] candidates)
    {
        var task = new AgentTask("t-bm25", AgentTaskType.Explore, prompt);
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
