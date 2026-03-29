using Microsoft.Extensions.Logging;
using nem.Contracts.Agents;
using nem.Contracts.Identity;
using nem.Contracts.Inference;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Agents.Selection;
using nem.Mimir.Application.Agents.Selection.Steps;
using NSubstitute;
using Shouldly;
using AppSpecialistAgent = nem.Mimir.Application.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Tests.Agents.Selection;

public sealed class EmbeddingRankerStepTests
{
    private readonly IInferenceGateway _inferenceGateway = Substitute.For<IInferenceGateway>();
    private readonly ILogger<EmbeddingRankerStep> _logger = Substitute.For<ILogger<EmbeddingRankerStep>>();

    [Fact]
    public async Task ExecuteAsync_ComputesCosineSimilarityAndRanks()
    {
        var codeAgent = CreateScoredAgent("code", "code agent", AgentCapability.CodeExploration);
        var webAgent = CreateScoredAgent("web", "web agent", AgentCapability.WebResearch);

        _inferenceGateway
            .SendAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                CreateResponse("[1,0]"),
                CreateResponse("[1,0]"),
                CreateResponse("[0,1]"));

        var sut = new EmbeddingRankerStep(_inferenceGateway, _logger);
        var context = CreateContext("refactor this method", codeAgent, webAgent);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.Candidates.Count.ShouldBe(2);
        result.Candidates[0].Agent.Name.ShouldBe("code");
        result.Candidates[0].StepScores["embedding"].ShouldBe(1d);
        result.Candidates[1].StepScores["embedding"].ShouldBe(0d);

        await _inferenceGateway.Received(3).SendAsync(
            Arg.Is<InferenceRequest>(x => x.Alias == InferenceModelAlias.Embedding),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_OnEmbeddingFailure_ReturnsContextUnchanged()
    {
        var candidate = CreateScoredAgent("code", "code agent", AgentCapability.CodeExploration);
        var context = CreateContext("refactor", candidate);

        _inferenceGateway
            .SendAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<InferenceResponse>>(_ => throw new InvalidOperationException("embedding unavailable"));

        var sut = new EmbeddingRankerStep(_inferenceGateway, _logger);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        ReferenceEquals(result, context).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaskEmbeddingCannotBeParsed_ReturnsContextUnchanged()
    {
        var candidate = CreateScoredAgent("code", "code agent", AgentCapability.CodeExploration);
        var context = CreateContext("refactor", candidate);

        _inferenceGateway
            .SendAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateResponse("not-json"));

        var sut = new EmbeddingRankerStep(_inferenceGateway, _logger);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        ReferenceEquals(result, context).ShouldBeTrue();
    }

    private static SelectionContext CreateContext(string prompt, params ScoredAgent[] candidates)
    {
        var task = new AgentTask("t-emb", AgentTaskType.Analyze, prompt);
        return new SelectionContext(task, candidates);
    }

    private static ScoredAgent CreateScoredAgent(string name, string description, params AgentCapability[] capabilities)
    {
        return ScoredAgent.Create(new TestAgent(name, description, capabilities));
    }

    private static InferenceResponse CreateResponse(string content)
    {
        return new InferenceResponse(
            content,
            InferenceModelAlias.Embedding,
            InferenceProviderId.New(),
            "embedding-model",
            ModelTier.Fast,
            CorrelationId.New(),
            0,
            0,
            0,
            "stop");
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
