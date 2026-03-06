using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Application.Knowledge;
using Mimir.Application.Services.Memory;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;
using nem.Contracts.Memory;

namespace Mimir.Application.Tests.Memory;

public sealed class EpisodicMemoryServiceTests
{
    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IKnowHubBridge _bridge = Substitute.For<IKnowHubBridge>();
    private readonly ILogger<EpisodicMemoryService> _logger = Substitute.For<ILogger<EpisodicMemoryService>>();

    [Fact]
    public async Task SaveEpisodeAsync_Persists_WhenBridgeUnavailable()
    {
        EpisodicMemoryService.ResetCacheForTesting();
        _bridge.IsAvailable.Returns(false);

        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "episodic");
        AddMessage(conversation, MessageRole.User, "hello", DateTimeOffset.UtcNow.AddMinutes(-2));
        AddMessage(conversation, MessageRole.Assistant, "world", DateTimeOffset.UtcNow.AddMinutes(-1));

        _repository.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        _repository.GetByUserIdAsync(userId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Conversation>(new[] { conversation }, 1, 1, 1));

        var sut = CreateSut();
        var episode = new Episode(
            Id: Guid.NewGuid().ToString("N"),
            UserId: userId.ToString(),
            ConversationId: conversation.Id.ToString(),
            Summary: "Summary text",
            KeyTopics: ["topic-a", "topic-b"],
            StartedAt: DateTimeOffset.UtcNow.AddMinutes(-2),
            EndedAt: DateTimeOffset.UtcNow,
            MessageCount: 2);

        await sut.SaveEpisodeAsync(episode, CancellationToken.None);

        await _repository.Received(1).UpdateAsync(conversation, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        var stored = await sut.GetEpisodesAsync(userId.ToString(), 10, CancellationToken.None);
        stored.Count.ShouldBe(1);
        stored[0].Summary.ShouldBe("Summary text");
    }

    [Fact]
    public async Task SearchSimilarAsync_ReturnsEmpty_WhenBridgeUnavailable()
    {
        EpisodicMemoryService.ResetCacheForTesting();
        _bridge.IsAvailable.Returns(false);
        var sut = CreateSut();

        var result = await sut.SearchSimilarAsync("find this", 5, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task SearchSimilarAsync_ReturnsRankedEpisodes_FromLocalEmbeddings()
    {
        EpisodicMemoryService.ResetCacheForTesting();
        _bridge.IsAvailable.Returns(true);

        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "embeddings");
        AddMessage(conversation, MessageRole.User, "message one", DateTimeOffset.UtcNow.AddMinutes(-2));
        AddMessage(conversation, MessageRole.Assistant, "message two", DateTimeOffset.UtcNow.AddMinutes(-1));

        _repository.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        _repository.GetByUserIdAsync(userId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Conversation>(new[] { conversation }, 1, 1, 1));

        _bridge.GenerateEmbeddingAsync("Episode A", Arg.Any<CancellationToken>())
            .Returns(new float[] { 1f, 0f, 0f, 0f });
        _bridge.GenerateEmbeddingAsync("Episode B", Arg.Any<CancellationToken>())
            .Returns(new float[] { 0f, 1f, 0f, 0f });
        _bridge.GenerateEmbeddingAsync("query", Arg.Any<CancellationToken>())
            .Returns(new float[] { 1f, 0f, 0f, 0f });
        _bridge.SearchKnowledgeAsync("query", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<KnowledgeSearchResult>());

        var sut = CreateSut(new EpisodicMemoryOptions { EmbeddingDimension = 4, MinQualityScore = 0.1f });

        await sut.SaveEpisodeAsync(new Episode(Guid.NewGuid().ToString("N"), userId.ToString(), conversation.Id.ToString(), "Episode A", ["alpha"], DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(-1), 2));
        await sut.SaveEpisodeAsync(new Episode(Guid.NewGuid().ToString("N"), userId.ToString(), conversation.Id.ToString(), "Episode B", ["beta"], DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow, 2));

        var result = await sut.SearchSimilarAsync("query", 2, CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].Summary.ShouldBe("Episode A");
    }

    [Fact]
    public async Task SummarizeSessionAsync_BuildsAndPersistsEpisode()
    {
        EpisodicMemoryService.ResetCacheForTesting();
        _bridge.IsAvailable.Returns(true);

        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "planning");
        AddMessage(conversation, MessageRole.User, "Need deployment plan", DateTimeOffset.UtcNow.AddMinutes(-3));
        AddMessage(conversation, MessageRole.Assistant, "We should do blue-green", DateTimeOffset.UtcNow.AddMinutes(-2));

        _repository.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);
        _repository.GetByUserIdAsync(userId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<Conversation>(new[] { conversation }, 1, 1, 1));

        _bridge.DistillKnowledgeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new DistilledKnowledge(Guid.NewGuid(), "Deployment strategy and rollout sequencing.", 0.92d, "deployment", "content")
            });
        _bridge.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.5f, 0.5f, 0.5f, 0.5f });

        var sut = CreateSut(new EpisodicMemoryOptions { EmbeddingDimension = 4, MinQualityScore = 0.4f });

        var summary = await sut.SummarizeSessionAsync(conversation.Id.ToString(), CancellationToken.None);

        summary.ShouldContain("Deployment strategy");

        var episodes = await sut.GetEpisodesAsync(userId.ToString(), 10, CancellationToken.None);
        episodes.Count.ShouldBe(1);
        episodes[0].MessageCount.ShouldBe(2);
        episodes[0].KeyTopics.ShouldContain("deployment");
    }

    private EpisodicMemoryService CreateSut(EpisodicMemoryOptions? options = null)
    {
        return new EpisodicMemoryService(
            _repository,
            _unitOfWork,
            _bridge,
            options ?? new EpisodicMemoryOptions(),
            _logger);
    }

    private static Message AddMessage(Conversation conversation, MessageRole role, string content, DateTimeOffset createdAt)
    {
        var message = conversation.AddMessage(role, content);
        message.SetTokenCount(10);

        var createdAtProperty = typeof(Message).GetProperty(nameof(Message.CreatedAt));
        createdAtProperty!.SetValue(message, createdAt);
        return message;
    }
}
