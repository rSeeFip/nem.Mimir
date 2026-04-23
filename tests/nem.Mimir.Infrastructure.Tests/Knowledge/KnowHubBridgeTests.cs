using System.Reflection;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Knowledge;
using nem.Mimir.Infrastructure.Knowledge;
using nem.KnowHub.Enhancement.Abstractions.Interfaces;
using nem.KnowHub.Enhancement.Abstractions.Models;
using nem.KnowHub.Enhancement.Distillation.Interfaces;
using nem.KnowHub.Enhancement.Distillation.Models;
using nem.KnowHub.Enhancement.GraphRag.Interfaces;
using nem.KnowHub.Enhancement.GraphRag.Models;
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
        result[0].OriginLink.ShouldBeNull();
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
    public void SourceOriginLinkDto_ToMarkdown_UsesExpectedFormatting()
    {
        new SourceOriginLinkDto("https://media/doc", "Document", "spec.pdf").ToMarkdown()
            .ShouldBe("[📄 spec.pdf](https://media/doc)");
        new SourceOriginLinkDto("https://media/code", "Code", "Program.cs", 27, 31).ToMarkdown()
            .ShouldBe("[💻 Program.cs:27-31](https://media/code)");
        new SourceOriginLinkDto("https://media/cad", "Cad", "layout.ifc").ToMarkdown()
            .ShouldBe("[📐 layout.ifc](https://media/cad)");
        new SourceOriginLinkDto("https://media/audio", "Audio", "meeting.wav").ToMarkdown()
            .ShouldBe("[🎤 meeting.wav](https://media/audio)");
        new SourceOriginLinkDto("https://media/other", "Media", "preview.png").ToMarkdown()
            .ShouldBe("[📎 preview.png](https://media/other)");
    }

    [Fact]
    public async Task SearchKnowledgeAsync_WhenOriginMetadataExists_MapsOriginLink()
    {
        var sut = CreateSut();

        _embedding.EmbedAsync("query", null, Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([0.1f, 0.2f], "test", 2));

        _vectorSearch.SearchAsync(Arg.Any<float[]>(), 5, null, 0.7f, Arg.Any<CancellationToken>())
            .Returns(new List<VectorSearchResult>
            {
                new(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "CodeChunk",
                    "auth-service",
                    "chunk",
                    0.91f,
                    OriginUrl: "https://mediahub/files/auth-service",
                    OriginFileName: "AuthService.cs",
                    OriginContentType: "text/x-csharp",
                    OriginKind: "Code",
                    OriginStartLine: 45,
                    OriginEndLine: 78),
            });

        var result = await sut.SearchKnowledgeAsync("query", 5, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].OriginLink.ShouldNotBeNull();
        result[0].OriginLink!.ToMarkdown().ShouldBe("[💻 AuthService.cs:45-78](https://mediahub/files/auth-service)");
    }

    [Fact]
    public async Task SearchKnowledgeAsync_WhenOriginKindMissing_InferOriginTypeFromMetadata()
    {
        var sut = CreateSut();

        _embedding.EmbedAsync("query", null, Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult([0.1f, 0.2f], "test", 2));

        _vectorSearch.SearchAsync(Arg.Any<float[]>(), 5, null, 0.7f, Arg.Any<CancellationToken>())
            .Returns(new List<VectorSearchResult>
            {
                new(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Drawing",
                    "layout",
                    "chunk",
                    0.87f,
                    OriginUrl: "https://mediahub/files/layout",
                    OriginFileName: "layout.dwg",
                    OriginContentType: "application/acad"),
            });

        var result = await sut.SearchKnowledgeAsync("query", 5, TestContext.Current.CancellationToken);

        result[0].OriginLink.ShouldNotBeNull();
        result[0].OriginLink!.OriginType.ShouldBe("Cad");
        result[0].OriginLink!.ToMarkdown().ShouldBe("[📐 layout.dwg](https://mediahub/files/layout)");
    }

    [Fact]
    public void KnowledgeSearchResult_HasNullableOriginLinkProperty()
    {
        var property = typeof(KnowledgeSearchResult).GetProperty(nameof(KnowledgeSearchResult.OriginLink), BindingFlags.Public | BindingFlags.Instance);

        property.ShouldNotBeNull();
        property!.PropertyType.ShouldBe(typeof(SourceOriginLinkDto));
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
