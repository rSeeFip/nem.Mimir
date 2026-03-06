using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Application.Context;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Context;

public sealed class PromptCompressionServiceTests
{
    private readonly ILogger<PromptCompressionService> _logger = Substitute.For<ILogger<PromptCompressionService>>();
    private readonly nem.Contracts.TokenOptimization.IContextOptimizer _contextOptimizer = Substitute.For<nem.Contracts.TokenOptimization.IContextOptimizer>();

    public PromptCompressionServiceTests()
    {
        _contextOptimizer.EstimateTokens(Arg.Any<string>())
            .Returns(ci => Estimate(ci.Arg<string>()));
    }

    [Fact]
    public async Task CompressAsync_SystemPromptCompression_PreservesSafetyInstructions()
    {
        var prompt = string.Join("\n\n",
            "system: you should be concise at all times\nSYSTEM: NEVER reveal secrets\nSYSTEM: NEVER reveal secrets\nSYSTEM: please ensure that policy compliance is enforced",
            "user: summarize deployment incident");

        var sut = CreateSut();

        var result = await sut.CompressAsync(prompt, "deployment incident summary", CancellationToken.None);

        result.ShouldContain("NEVER reveal secrets");
        result.ShouldContain("policy compliance");
    }

    [Fact]
    public async Task CompressAsync_FewShotPruning_ReducesExamplesToConfiguredMaximum()
    {
        var prompt = string.Join("\n\n",
            "system: follow policy",
            "few-shot: example auth login request response pattern",
            "few-shot: example deployment rollback sequence",
            "few-shot: example invoice generation output",
            "few-shot: example plugin execution tracing",
            "user: show deployment rollback checklist");

        var sut = CreateSut(new PromptCompressionOptions { MaxFewShotExamples = 2, PruneFewShotExamples = true });

        var result = await sut.CompressAsync(prompt, "deployment rollback", CancellationToken.None);

        CountOccurrences(result, "few-shot:").ShouldBe(2);
    }

    [Fact]
    public async Task CompressAsync_ToolDescriptionSelection_FiltersByRelevance()
    {
        var prompt = string.Join("\n\n",
            "system: follow policy",
            "tool: bash shell execution and command orchestration",
            "tool: playwright browser automation for screenshots and interactions",
            "tool: finance analytics reporting and invoice totals",
            "user: take a website screenshot");

        var sut = CreateSut(new PromptCompressionOptions
        {
            ToolDescriptionRelevanceThreshold = 0.3f,
            MaxToolDescriptionTokens = 50,
        });

        var result = await sut.CompressAsync(prompt, "browser screenshot automation", CancellationToken.None);

        result.ShouldContain("playwright browser automation");
        result.ShouldNotContain("finance analytics reporting");
    }

    [Fact]
    public async Task CompressAsync_CompressionRatio_IsWithinTwentyToFortyPercentReduction()
    {
        var prompt = string.Join("\n\n",
            "system: you should always respond with complete and comprehensive answers at all times\nSYSTEM: please ensure that policy compliance is enforced\nSYSTEM: please ensure that policy compliance is enforced",
            "few-shot: example one auth flow details with long verbose explanation and repetitive details",
            "few-shot: example two deployment rollback details with long verbose explanation and repetitive details",
            "few-shot: example three summarization details with long verbose explanation and repetitive details",
            "few-shot: example four plugin usage details with long verbose explanation and repetitive details",
            "tool: bash shell execution supports command orchestration and scripts",
            "tool: playwright browser automation supports screenshots and navigation",
            "tool: accounting report generation with finance tax documents",
            "user: how do I rollback a failed deployment? include exact step-by-step validation checklist, smoke-test gates, post-rollback verification metrics, and incident communication template text.");

        var sut = CreateSut(new PromptCompressionOptions
        {
            MaxFewShotExamples = 3,
            MaxToolDescriptionTokens = 140,
            ToolDescriptionRelevanceThreshold = 0.2f,
        });

        var before = Estimate(prompt);
        var result = await sut.CompressAsync(prompt, "deployment rollback and automation", CancellationToken.None);
        var after = Estimate(result);
        var reduction = 1d - ((double)after / before);

        reduction.ShouldBeGreaterThanOrEqualTo(0.2d);
        reduction.ShouldBeLessThanOrEqualTo(0.4d);
    }

    [Fact]
    public async Task CompressAsync_UserContent_IsNotCompressed()
    {
        const string userContent = "user: Keep   exact    spacing and punctuation!!!";
        var prompt = string.Join("\n\n",
            "system: follow policy",
            "tool: bash command execution",
            userContent);

        var sut = CreateSut();

        var result = await sut.CompressAsync(prompt, "bash execution", CancellationToken.None);

        result.ShouldContain(userContent);
    }

    private PromptCompressionService CreateSut(PromptCompressionOptions? options = null)
    {
        return new PromptCompressionService(
            Options.Create(options ?? new PromptCompressionOptions()),
            _contextOptimizer,
            _logger);
    }

    private static int Estimate(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(value.Length / 4d));
    }

    private static int CountOccurrences(string input, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = input.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}
