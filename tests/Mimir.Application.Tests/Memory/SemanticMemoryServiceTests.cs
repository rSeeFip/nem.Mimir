using Microsoft.Extensions.Logging;
using Mimir.Application.Knowledge;
using Mimir.Application.Services.Memory;
using NSubstitute;
using Shouldly;
using nem.Contracts.Memory;

namespace Mimir.Application.Tests.Memory;

public sealed class SemanticMemoryServiceTests
{
    private readonly IKnowHubBridge _bridge = Substitute.For<IKnowHubBridge>();
    private readonly ILogger<SemanticMemoryService> _logger = Substitute.For<ILogger<SemanticMemoryService>>();

    [Fact]
    public async Task StoreFactAsync_PersistsFact_WhenBridgeUnavailable()
    {
        _bridge.IsAvailable.Returns(false);
        var repository = new InMemorySemanticFactRepository();
        var sut = CreateSut(repository);
        var fact = new KnowledgeFact("fact-1", "Ada", "knows", "C#", 0.9f);

        await sut.StoreFactAsync(fact, CancellationToken.None);

        var stored = await repository.GetByFactIdAsync("fact-1", CancellationToken.None);
        stored.ShouldNotBeNull();
        stored!.EmbeddingVector.ShouldBeNull();
    }

    [Fact]
    public async Task QueryAsync_UsesBridgeAndReturnsRankedFacts_WhenAvailable()
    {
        _bridge.IsAvailable.Returns(true);
        _bridge.SearchKnowledgeAsync("csharp", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new KnowledgeSearchResult(Guid.NewGuid(), "Ada knows csharp deeply", 0.95f),
                new KnowledgeSearchResult(Guid.NewGuid(), "Grace likes cobol", 0.2f)
            });

        var repository = new InMemorySemanticFactRepository();
        var sut = CreateSut(repository);

        await sut.StoreFactAsync(new KnowledgeFact("f1", "Ada", "knows", "CSharp", 0.9f), CancellationToken.None);
        await sut.StoreFactAsync(new KnowledgeFact("f2", "Grace", "likes", "Cobol", 0.9f), CancellationToken.None);

        var result = await sut.QueryAsync("csharp", 5, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("f1");
    }

    [Fact]
    public async Task QueryAsync_FallsBackToTextMatching_WhenBridgeUnavailable()
    {
        _bridge.IsAvailable.Returns(false);
        var repository = new InMemorySemanticFactRepository();
        var sut = CreateSut(repository);

        await sut.StoreFactAsync(new KnowledgeFact("f1", "Azure", "supports", "Managed Identity", 0.8f), CancellationToken.None);
        await sut.StoreFactAsync(new KnowledgeFact("f2", "Docker", "runs", "Containers", 0.8f), CancellationToken.None);

        var result = await sut.QueryAsync("managed identity", 10, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("f1");
    }

    [Fact]
    public async Task GetRelatedAsync_ReturnsEmpty_WhenBridgeUnavailable()
    {
        _bridge.IsAvailable.Returns(false);
        var repository = new InMemorySemanticFactRepository();
        var sut = CreateSut(repository);

        await sut.StoreFactAsync(new KnowledgeFact("root", "Mimir", "uses", "KnowHub", 0.9f), CancellationToken.None);

        var result = await sut.GetRelatedAsync("root", 5, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetRelatedAsync_UsesGraphBridgeAndReturnsMatches()
    {
        _bridge.IsAvailable.Returns(true);
        _bridge.QueryGraphAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new GraphQueryResult("ok", "graph", new[] { "Knowledge" }, Array.Empty<string>(), Array.Empty<string>(), 0.8));

        var repository = new InMemorySemanticFactRepository();
        var sut = CreateSut(repository);

        await sut.StoreFactAsync(new KnowledgeFact("root", "Mimir", "integrates", "Knowledge", 0.9f), CancellationToken.None);
        await sut.StoreFactAsync(new KnowledgeFact("child", "Knowledge", "servedBy", "KnowHub", 0.85f), CancellationToken.None);

        var result = await sut.GetRelatedAsync("root", 5, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("child");
    }

    [Fact]
    public async Task UpdateFactAsync_Throws_WhenFactMissing()
    {
        _bridge.IsAvailable.Returns(false);
        var repository = new InMemorySemanticFactRepository();
        var sut = CreateSut(repository);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.UpdateFactAsync(new KnowledgeFact("missing", "A", "is", "B", 0.8f), CancellationToken.None));
    }

    [Fact]
    public async Task GetGraphAsync_ReturnsEmpty_WhenBridgeUnavailable()
    {
        _bridge.IsAvailable.Returns(false);
        var repository = new InMemorySemanticFactRepository();
        var sut = CreateSut(repository);

        await sut.StoreFactAsync(new KnowledgeFact("root", "A", "links", "B", 0.8f), CancellationToken.None);

        var result = await sut.GetGraphAsync("root", 2, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetGraphAsync_ReturnsSubgraph_WhenBridgeAvailable()
    {
        _bridge.IsAvailable.Returns(true);
        _bridge.QueryGraphAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new GraphQueryResult("ok", "graph", new[] { "NodeB" }, Array.Empty<string>(), Array.Empty<string>(), 1.0));

        var repository = new InMemorySemanticFactRepository();
        var sut = CreateSut(repository);

        await sut.StoreFactAsync(new KnowledgeFact("f1", "NodeA", "connects", "NodeB", 0.9f), CancellationToken.None);
        await sut.StoreFactAsync(new KnowledgeFact("f2", "NodeB", "connects", "NodeC", 0.9f), CancellationToken.None);

        var result = await sut.GetGraphAsync("f1", 2, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThanOrEqualTo(1);
        result.Any(x => x.Id == "f1").ShouldBeTrue();
    }

    private SemanticMemoryService CreateSut(InMemorySemanticFactRepository repository, SemanticMemoryOptions? options = null)
    {
        return new SemanticMemoryService(
            repository,
            _bridge,
            options ?? new SemanticMemoryOptions(),
            _logger);
    }
}
