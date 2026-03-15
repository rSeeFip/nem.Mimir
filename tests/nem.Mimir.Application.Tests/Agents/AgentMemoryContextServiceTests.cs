using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Agents;
using NSubstitute;
using Shouldly;
using nem.Contracts.Memory;

namespace nem.Mimir.Application.Tests.Agents;

public sealed class AgentMemoryContextServiceTests
{
    private readonly IWorkingMemory _workingMemory = Substitute.For<IWorkingMemory>();
    private readonly IEpisodicMemory _episodicMemory = Substitute.For<IEpisodicMemory>();
    private readonly ISemanticMemory _semanticMemory = Substitute.For<ISemanticMemory>();
    private readonly ILogger<AgentMemoryContextService> _logger = Substitute.For<ILogger<AgentMemoryContextService>>();

    [Fact]
    public async Task AssembleContextAsync_ForGeneralAgent_IncludesAllMemoryStores()
    {
        _semanticMemory.QueryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new KnowledgeFact("s1", "Ada", "knows", "C#", 0.95f),
            ]);

        _episodicMemory.SearchSimilarAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new Episode("e1", "u1", "c1", "User asked about deployment", ["deployment"], DateTimeOffset.UtcNow.AddMinutes(-30), DateTimeOffset.UtcNow, 8),
            ]);

        _workingMemory.GetRecentAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new ConversationMessage("user", "What is the rollout plan?", DateTimeOffset.UtcNow),
            ]);

        var sut = CreateSut();

        var result = await sut.AssembleContextAsync("c1", "u1", "general", 1000, CancellationToken.None);

        result.SemanticFacts.Count.ShouldBe(1);
        result.EpisodicSummaries.Count.ShouldBe(1);
        result.WorkingMessages.Count.ShouldBe(1);
        result.TotalTokenEstimate.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AssembleContextAsync_ForExecuteAgent_IncludesOnlyWorkingMemory()
    {
        _workingMemory.GetRecentAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new ConversationMessage("assistant", "Executing build step now", DateTimeOffset.UtcNow),
            ]);

        var sut = CreateSut();

        var result = await sut.AssembleContextAsync("c1", "u1", "execute", 1000, CancellationToken.None);

        result.SemanticFacts.ShouldBeEmpty();
        result.EpisodicSummaries.ShouldBeEmpty();
        result.WorkingMessages.Count.ShouldBe(1);

        await _semanticMemory.DidNotReceiveWithAnyArgs().QueryAsync(default!, default, default);
        await _episodicMemory.DidNotReceiveWithAnyArgs().SearchSimilarAsync(default!, default, default);
        await _workingMemory.Received(1).GetRecentAsync("c1", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssembleContextAsync_ForExploreAgent_IncludesFullSemanticAccess()
    {
        _semanticMemory.QueryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 10)
                .Select(i => new KnowledgeFact($"s{i}", "topic", "is", $"fact-{i}", 0.9f))
                .ToList());

        _episodicMemory.SearchSimilarAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new Episode("e1", "u1", "c1", "Some prior episode", ["x"], DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow, 3),
            ]);

        var sut = CreateSut();

        var result = await sut.AssembleContextAsync("c1", "u1", "explore", 1000, CancellationToken.None);

        result.SemanticFacts.Count.ShouldBeGreaterThan(0);
        result.WorkingMessages.ShouldBeEmpty();

        await _semanticMemory.Received(1).QueryAsync("user:u1", Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _episodicMemory.Received(1).SearchSimilarAsync("u1", Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _workingMemory.DidNotReceiveWithAnyArgs().GetRecentAsync(default!, default, default);
    }

    [Fact]
    public async Task AssembleContextAsync_WithMemoryTimeout_ReturnsPartialResults()
    {
        _semanticMemory.QueryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new KnowledgeFact("s1", "A", "rel", "B", 0.8f),
            ]);

        _episodicMemory.SearchSimilarAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => DelayedEpisodesAsync());

        _workingMemory.GetRecentAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new ConversationMessage("user", "Latest message", DateTimeOffset.UtcNow),
            ]);

        var sut = CreateSut(new AgentMemoryContextOptions
        {
            MemoryRetrievalTimeoutMs = 25,
            SemanticBudgetPercent = 20,
            EpisodicBudgetPercent = 30,
            WorkingBudgetPercent = 50,
            EstimatedTokensPerChar = 0.25f,
        });

        var result = await sut.AssembleContextAsync("c1", "u1", "general", 1000, CancellationToken.None);

        result.SemanticFacts.Count.ShouldBeGreaterThan(0);
        result.WorkingMessages.Count.ShouldBeGreaterThan(0);
        result.EpisodicSummaries.ShouldBeEmpty();
    }

    [Fact]
    public async Task AssembleContextAsync_RespectsBudgetAllocation()
    {
        _semanticMemory.QueryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 10)
                .Select(i => new KnowledgeFact($"s{i}", "subject", "predicate", "object with enough length to consume budget", 0.9f))
                .ToList());

        _episodicMemory.SearchSimilarAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 10)
                .Select(i => new Episode($"e{i}", "u1", "c1", "episodic summary with enough characters to consume budget", ["topic"], DateTimeOffset.UtcNow.AddMinutes(-20), DateTimeOffset.UtcNow, 5))
                .ToList());

        _workingMemory.GetRecentAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 10)
                .Select(i => new ConversationMessage("user", "working memory line with enough characters to consume budget", DateTimeOffset.UtcNow))
                .ToList());

        var options = new AgentMemoryContextOptions
        {
            SemanticBudgetPercent = 20,
            EpisodicBudgetPercent = 30,
            WorkingBudgetPercent = 50,
            EstimatedTokensPerChar = 1f,
            MemoryRetrievalTimeoutMs = 200,
        };

        var sut = CreateSut(options);
        var result = await sut.AssembleContextAsync("c1", "u1", "default", 100, CancellationToken.None);

        EstimateTokens(result.SemanticFacts, options.EstimatedTokensPerChar).ShouldBeLessThanOrEqualTo(20);
        EstimateTokens(result.EpisodicSummaries, options.EstimatedTokensPerChar).ShouldBeLessThanOrEqualTo(30);
        EstimateTokens(result.WorkingMessages, options.EstimatedTokensPerChar).ShouldBeLessThanOrEqualTo(50);
        result.TotalTokenEstimate.ShouldBeLessThanOrEqualTo(100);
    }

    private AgentMemoryContextService CreateSut(AgentMemoryContextOptions? options = null)
    {
        return new AgentMemoryContextService(
            _workingMemory,
            _episodicMemory,
            _semanticMemory,
            options ?? new AgentMemoryContextOptions(),
            _logger);
    }

    private static int EstimateTokens(IReadOnlyList<string> lines, float tokensPerChar)
    {
        return lines.Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length * tokensPerChar)));
    }

    private static async Task<IReadOnlyList<Episode>> DelayedEpisodesAsync()
    {
        await Task.Delay(200);
        return
        [
            new Episode("e1", "u1", "c1", "Delayed episode", ["late"], DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow, 2),
        ];
    }
}
