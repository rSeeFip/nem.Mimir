using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Context;
using NSubstitute;
using Shouldly;
using nem.Contracts.TokenOptimization;

namespace nem.Mimir.Application.Tests.Context;

public sealed class ContextOptimizerServiceTests
{
    private readonly ILogger<ContextOptimizerService> _logger = Substitute.For<ILogger<ContextOptimizerService>>();

    [Fact]
    public async Task DistillAsync_ReducesTokensToTargetBudget()
    {
        var content = BuildConversation(14);
        var options = new ContextOptimizerOptions
        {
            MaxContextTokens = 120,
            AutoTriggerThreshold = 0.8,
            CharsPerToken = 4.0,
            TargetCompressionRatio = 0.5,
        };
        var sut = CreateSut(options);

        var distilled = await sut.DistillAsync(content, targetTokens: 30, CancellationToken.None);

        sut.EstimateTokens(distilled).ShouldBeLessThanOrEqualTo(30);
        distilled.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PruneAsync_RemoveOldest_RemovesEarlyBlocksAndKeepsRecent()
    {
        var content = BuildConversation(18);
        var sut = CreateSut(new ContextOptimizerOptions
        {
            MaxContextTokens = 40,
            AutoTriggerThreshold = 0.8,
            TargetCompressionRatio = 0.4,
            CharsPerToken = 4.0,
        });

        var pruned = await sut.PruneAsync(content, PruneStrategy.RemoveOldest, CancellationToken.None);

        pruned.ShouldContain("message-18");
        pruned.ShouldNotContain("user: message-1 with deployment context");
        sut.EstimateTokens(pruned).ShouldBeLessThan(sut.EstimateTokens(content));
    }

    [Fact]
    public async Task PruneAsync_RemoveLeastRelevant_RemovesLowPriorityNoise()
    {
        var content = string.Join("\n\n",
            "system: follow policies",
            "user: high priority deployment failure root cause and mitigation",
            "assistant: acknowledged",
            "noise aaa bbb ccc ddd",
            "noise eee fff ggg hhh",
            "user: latest critical production incident status",
            "assistant: preparing rollback plan");

        var sut = CreateSut(new ContextOptimizerOptions
        {
            MaxContextTokens = 45,
            AutoTriggerThreshold = 0.8,
            TargetCompressionRatio = 0.4,
            CharsPerToken = 4.0,
        });

        var pruned = await sut.PruneAsync(content, PruneStrategy.RemoveLeastRelevant, CancellationToken.None);

        pruned.ShouldContain("latest critical production incident status");
        pruned.ShouldNotContain("noise aaa bbb ccc ddd");
    }

    [Fact]
    public async Task PruneAsync_Summarize_UsesDistillationPath()
    {
        var content = BuildConversation(16);
        var sut = CreateSut(new ContextOptimizerOptions
        {
            MaxContextTokens = 45,
            AutoTriggerThreshold = 0.8,
            TargetCompressionRatio = 0.5,
            CharsPerToken = 4.0,
        });

        var summarized = await sut.PruneAsync(content, PruneStrategy.Summarize, CancellationToken.None);

        sut.EstimateTokens(summarized).ShouldBeLessThan(sut.EstimateTokens(content));
        summarized.ShouldContain("message-16");
    }

    [Fact]
    public async Task PruneAsync_Truncate_PreservesSystemAndRecentBlocks()
    {
        var content = string.Join("\n\n",
            "system: mandatory policy context",
            "user: message-1 lorem ipsum alpha beta gamma",
            "assistant: message-2 lorem ipsum alpha beta gamma",
            "user: message-3 lorem ipsum alpha beta gamma",
            "assistant: message-4 lorem ipsum alpha beta gamma",
            "user: message-5 lorem ipsum alpha beta gamma",
            "assistant: message-6 lorem ipsum alpha beta gamma");

        var sut = CreateSut(new ContextOptimizerOptions
        {
            MaxContextTokens = 30,
            AutoTriggerThreshold = 0.8,
            TargetCompressionRatio = 0.4,
            CharsPerToken = 4.0,
        });

        var truncated = await sut.PruneAsync(content, PruneStrategy.Truncate, CancellationToken.None);

        truncated.ShouldContain("system: mandatory policy context");
        truncated.ShouldContain("message-6");
        truncated.ShouldContain("message-5");
    }

    [Fact]
    public void EstimateTokens_UsesConfiguredCharsPerToken()
    {
        var sut = CreateSut(new ContextOptimizerOptions
        {
            CharsPerToken = 4.0,
        });

        var tokens = sut.EstimateTokens("12345678");

        tokens.ShouldBe(2);
    }

    [Fact]
    public async Task CompressAsync_CompactsWhitespaceAndDuplicateLines()
    {
        var content = string.Join("\n\n",
            "system: keep",
            "user:    duplicate    text    here",
            "user: duplicate text here",
            "assistant:   lots   of   spacing");

        var sut = CreateSut(new ContextOptimizerOptions
        {
            MaxContextTokens = 12,
            AutoTriggerThreshold = 0.8,
            TargetCompressionRatio = 0.5,
            CharsPerToken = 4.0,
        });

        var compressed = await sut.CompressAsync(content, CancellationToken.None);

        compressed.ShouldContain("system: keep");
        compressed.ShouldContain("assistant: lots of spacing");
        compressed.ShouldNotContain("    ");
    }

    [Fact]
    public async Task PruneAsync_DoesNotTriggerBelowThreshold_ReturnsOriginal()
    {
        var content = "user: short";
        var sut = CreateSut(new ContextOptimizerOptions
        {
            MaxContextTokens = 100,
            AutoTriggerThreshold = 0.8,
            TargetCompressionRatio = 0.5,
            CharsPerToken = 4.0,
        });

        var result = await sut.PruneAsync(content, PruneStrategy.RemoveOldest, CancellationToken.None);

        result.ShouldBe(content);
    }

    private ContextOptimizerService CreateSut(ContextOptimizerOptions? options = null)
    {
        return new ContextOptimizerService(options ?? new ContextOptimizerOptions(), _logger);
    }

    private static string BuildConversation(int count)
    {
        var lines = new List<string> { "system: platform policy baseline" };
        for (var i = 1; i <= count; i++)
        {
            var role = i % 2 == 0 ? "assistant" : "user";
            lines.Add($"{role}: message-{i} with deployment context and troubleshooting details for iteration {i}");
        }

        return string.Join("\n\n", lines);
    }
}
