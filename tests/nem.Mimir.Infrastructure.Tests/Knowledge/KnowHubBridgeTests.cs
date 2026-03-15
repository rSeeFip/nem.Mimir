using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Knowledge;
using nem.Mimir.Infrastructure.Knowledge;
using nem.KnowHub.Abstractions.Interfaces;
using nem.KnowHub.Abstractions.Models;
using nem.KnowHub.Distillation.Interfaces;
using nem.KnowHub.Distillation.Models;
using nem.KnowHub.GraphRag.Interfaces;
using nem.KnowHub.GraphRag.Models;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Knowledge;

public sealed class KnowHubBridgeTests
{
    private readonly IVectorSearchService _vectorSearch;
    private readonly IKnowledgeDistillationService _distillation;
    private readonly IGraphRagSearcher _graphRag;
    private readonly IEmbeddingService _embedding;
    private readonly ILogger<KnowHubBridge> _logger;

    public KnowHubBridgeTests()
    {
        _vectorSearch = Substitute.For<IVectorSearchService>();
        _distillation = Substitute.For<IKnowledgeDistillationService>();
        _graphRag = Substitute.For<IGraphRagSearcher>();
        _embedding = Substitute.For<IEmbeddingService>();
        _logger = Substitute.For<ILogger<KnowHubBridge>>();
    }

    [Fact]
    public async Task SearchKnowledgeAsync_WhenServicesSucceed_ReturnsMappedResults()
    {
        var sut = CreateSut();

        _embedding.EmbedAsync("query", null, Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([0.1f, 0.2f], "test", 2));

        var contentId = Guid.NewGuid();
        _vectorSearch.SearchAsync(Arg.Any<float[]>(), 5, null, 0.7f, Arg.Any<CancellationToken>())
            .Returns(new List<VectorSearchResult>
            {
                new(Guid.NewGuid(), contentId, "GraphNode", "n1", "chunk", 0.91f),
            });

        var result = await sut.SearchKnowledgeAsync("query", 5);

        result.Count.ShouldBe(1);
        result[0].ContentId.ShouldBe(contentId);
        result[0].ChunkText.ShouldBe("chunk");
        result[0].Similarity.ShouldBe(0.91f);
        sut.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task DistillKnowledgeAsync_WhenServicesSucceed_ReturnsMappedResults()
    {
        var sut = CreateSut();

        _distillation.DistillAsync("content", "source", Arg.Any<CancellationToken>())
            .Returns(new List<DistillationResult>
            {
                new(Guid.NewGuid(), DistillationOutcome.Ingested, new KnowledgeScore(0.7, 0.8, 0.9, 0.85), KnowledgeType.Pattern, "distilled"),
            });

        var result = await sut.DistillKnowledgeAsync("content", "source");

        result.Count.ShouldBe(1);
        result[0].Outcome.ShouldBe("Ingested");
        result[0].CompositeScore.ShouldBe(0.85d);
        result[0].Type.ShouldBe("Pattern");
        result[0].Content.ShouldBe("distilled");
        sut.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryGraphAsync_WhenServicesSucceed_ReturnsMappedResult()
    {
        var sut = CreateSut();

        _graphRag.HybridSearchAsync("what", "tenant-1", 3, Arg.Any<CancellationToken>())
            .Returns(new GraphRagSearchResult(
                "answer",
                SearchMode.Hybrid,
                ["entity-a"],
                ["community-a"],
                ["ctx"],
                0.88));

        var result = await sut.QueryGraphAsync("what", "tenant-1", 3);

        result.Answer.ShouldBe("answer");
        result.Mode.ShouldBe("Hybrid");
        result.RelevantEntities.ShouldContain("entity-a");
        result.RelevantCommunities.ShouldContain("community-a");
        result.ContextChunks.ShouldContain("ctx");
        result.Score.ShouldBe(0.88d);
        sut.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchKnowledgeAsync_WhenKnowHubFails_GracefullyDegrades()
    {
        var sut = CreateSut();

        _embedding.EmbedAsync("query", null, Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([0.1f, 0.2f], "test", 2));

        _vectorSearch.SearchAsync(Arg.Any<float[]>(), 10, null, 0.7f, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<VectorSearchResult>>(new InvalidOperationException("down")));

        var result = await sut.SearchKnowledgeAsync("query");

        result.ShouldBeEmpty();
        sut.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryGraphAsync_WhenKnowHubFails_GracefullyDegrades()
    {
        var sut = CreateSut();

        _graphRag.HybridSearchAsync("what", "tenant-1", 10, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GraphRagSearchResult>(new InvalidOperationException("down")));

        var result = await sut.QueryGraphAsync("what", "tenant-1");

        result.Answer.ShouldBeEmpty();
        result.Mode.ShouldBe("Unavailable");
        result.ContextChunks.ShouldBeEmpty();
        result.Score.ShouldBe(0d);
        sut.IsAvailable.ShouldBeFalse();
    }

    private KnowHubBridge CreateSut() =>
        new(_vectorSearch, _distillation, _graphRag, _embedding, _logger);
}
