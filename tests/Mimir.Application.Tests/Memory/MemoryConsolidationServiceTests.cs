using Microsoft.Extensions.Logging;
using Mimir.Application.Knowledge;
using Mimir.Application.Services.Memory;
using NSubstitute;
using Shouldly;
using nem.Contracts.Memory;

namespace Mimir.Application.Tests.Memory;

public sealed class MemoryConsolidationServiceTests
{
    private readonly IWorkingMemory _workingMemory = Substitute.For<IWorkingMemory>();
    private readonly IEpisodicMemory _episodicMemory = Substitute.For<IEpisodicMemory>();
    private readonly ISemanticMemory _semanticMemory = Substitute.For<ISemanticMemory>();
    private readonly IKnowHubBridge _bridge = Substitute.For<IKnowHubBridge>();
    private readonly ILogger<MemoryConsolidationService> _logger = Substitute.For<ILogger<MemoryConsolidationService>>();

    [Fact]
    public async Task ConsolidateAsync_WithBalancedStrategy_ConsolidatesHighQualityOnly()
    {
        var conversationId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();

        _workingMemory.GetRecentAsync(conversationId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildMessageWithUserId(userId) });
        _episodicMemory.GetEpisodesAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                BuildEpisode("ep-high", userId, conversationId, "high quality"),
                BuildEpisode("ep-low", userId, conversationId, "low quality")
            });

        _bridge.IsAvailable.Returns(true);
        _bridge.DistillKnowledgeAsync("high quality", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DistilledKnowledge(Guid.NewGuid(), "outcome", 0.92d, "insight", "high fact") });
        _bridge.DistillKnowledgeAsync("low quality", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DistilledKnowledge(Guid.NewGuid(), "outcome", 0.31d, "insight", "low fact") });

        var sut = CreateSut(new MemoryConsolidationOptions { EpisodicToSemanticThreshold = 0.6f });

        await sut.ConsolidateAsync(conversationId, MemoryConsolidationStrategy.Balanced, CancellationToken.None);

        await _semanticMemory.Received(1)
            .StoreFactAsync(Arg.Is<KnowledgeFact>(x => x.Object == "high fact"), Arg.Any<CancellationToken>());
        await _semanticMemory.DidNotReceive()
            .StoreFactAsync(Arg.Is<KnowledgeFact>(x => x.Object == "low fact"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsolidateAsync_WithAggressiveStrategy_ConsolidatesAllEpisodes()
    {
        var conversationId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();

        _workingMemory.GetRecentAsync(conversationId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildMessageWithUserId(userId) });
        _episodicMemory.GetEpisodesAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                BuildEpisode("ep-1", userId, conversationId, "episode one"),
                BuildEpisode("ep-2", userId, conversationId, "episode two")
            });

        _bridge.IsAvailable.Returns(true);
        _bridge.DistillKnowledgeAsync("episode one", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DistilledKnowledge(Guid.NewGuid(), "o1", 0.12d, "type", "fact-1") });
        _bridge.DistillKnowledgeAsync("episode two", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DistilledKnowledge(Guid.NewGuid(), "o2", 0.21d, "type", "fact-2") });

        var sut = CreateSut();

        await sut.ConsolidateAsync(conversationId, MemoryConsolidationStrategy.Aggressive, CancellationToken.None);

        await _semanticMemory.Received(2).StoreFactAsync(Arg.Any<KnowledgeFact>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsolidateAsync_WithConservativeStrategy_SkipsUnflaggedEpisodes()
    {
        var conversationId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();

        _workingMemory.GetRecentAsync(conversationId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildMessageWithUserId(userId) });
        _episodicMemory.GetEpisodesAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                BuildEpisode("ep-flagged", userId, conversationId, "critical incident", ["flagged"]),
                BuildEpisode("ep-normal", userId, conversationId, "routine update", ["general"])
            });

        _bridge.IsAvailable.Returns(true);
        _bridge.DistillKnowledgeAsync("critical incident", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new DistilledKnowledge(Guid.NewGuid(), "o1", 0.8d, "type", "flagged-fact") });

        var sut = CreateSut();

        await sut.ConsolidateAsync(conversationId, MemoryConsolidationStrategy.Conservative, CancellationToken.None);

        await _bridge.Received(1).DistillKnowledgeAsync("critical incident", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _bridge.DidNotReceive().DistillKnowledgeAsync("routine update", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _semanticMemory.Received(1).StoreFactAsync(Arg.Any<KnowledgeFact>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsolidateAsync_WhenBridgeUnavailable_StillSavesEpisode()
    {
        var conversationId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();

        _workingMemory.GetRecentAsync(conversationId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildMessageWithUserId(userId) });
        _episodicMemory.GetEpisodesAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildEpisode("ep-1", userId, conversationId, "summary") });
        _bridge.IsAvailable.Returns(false);

        var sut = CreateSut();

        await sut.ConsolidateAsync(conversationId, MemoryConsolidationStrategy.Balanced, CancellationToken.None);

        await _episodicMemory.Received(1).SummarizeSessionAsync(conversationId, Arg.Any<CancellationToken>());
        await _semanticMemory.DidNotReceive().StoreFactAsync(Arg.Any<KnowledgeFact>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsolidateAsync_WorkingToEpisodic_CallsSummarizeSession()
    {
        var conversationId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();

        _workingMemory.GetRecentAsync(conversationId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { BuildMessageWithUserId(userId) });
        _episodicMemory.GetEpisodesAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Episode>());
        _bridge.IsAvailable.Returns(true);

        var sut = CreateSut();

        await sut.ConsolidateAsync(conversationId, MemoryConsolidationStrategy.Balanced, CancellationToken.None);

        await _episodicMemory.Received(1).SummarizeSessionAsync(conversationId, Arg.Any<CancellationToken>());
    }

    private MemoryConsolidationService CreateSut(MemoryConsolidationOptions? options = null)
    {
        return new MemoryConsolidationService(
            _workingMemory,
            _episodicMemory,
            _semanticMemory,
            _bridge,
            options ?? new MemoryConsolidationOptions(),
            _logger);
    }

    private static ConversationMessage BuildMessageWithUserId(string userId)
        => new("user", "hello", DateTimeOffset.UtcNow, new Dictionary<string, string> { ["userId"] = userId });

    private static Episode BuildEpisode(
        string episodeId,
        string userId,
        string conversationId,
        string summary,
        IReadOnlyList<string>? topics = null)
        => new(
            episodeId,
            userId,
            conversationId,
            summary,
            topics ?? ["topic"],
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow,
            4);
}
