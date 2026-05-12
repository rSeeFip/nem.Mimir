using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Tokens;
using nem.Mimir.Infrastructure.Tokens;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.E2E.Tests;

public sealed class TokenBudgetE2ETests
{
    // ─── Deny path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Budget_OverLimit_Deny_ReturnsErrorResponse()
    {
        var tracker = Substitute.For<ITokenTracker>();
        tracker
            .GetUsageAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new TokenUsageSummary("nem.mimir", 9_000, 1_000, 0m, 10));

        var options = Options.Create(new TokenBudgetGovernorOptions { DefaultBudget = 10_000 });
        var governor = new TokenBudgetGovernor(tracker, options);

        var innerLlm = Substitute.For<ILlmService>();
        var decorator = new BudgetLlmServiceDecorator(innerLlm, governor);

        var messages = new[] { new LlmMessage("user", "Hello, this will exceed the budget") };

        var response = await decorator.SendMessageAsync("qwen-2.5-72b", messages, CancellationToken.None);

        response.FinishReason.ShouldBe("budget_denied");
        response.Content.ShouldNotBeNullOrWhiteSpace();
        await innerLlm.DidNotReceive().SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>());
    }

    // ─── Warn path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Budget_NearLimit_Warn_ProceedsWithInnerCall()
    {
        var tracker = Substitute.For<ITokenTracker>();
        tracker
            .GetUsageAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new TokenUsageSummary("nem.mimir", 8_000, 500, 0m, 8));

        var options = Options.Create(new TokenBudgetGovernorOptions
        {
            DefaultBudget = 10_000,
            WarnThresholdPercent = 80.0,
        });
        var governor = new TokenBudgetGovernor(tracker, options);

        var innerLlm = Substitute.For<ILlmService>();
        innerLlm
            .SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Near-budget response", "qwen-2.5-72b", 10, 5, 15, "stop"));

        var decorator = new BudgetLlmServiceDecorator(innerLlm, governor);

        var messages = new[] { new LlmMessage("user", "Near budget request") };

        var response = await decorator.SendMessageAsync("qwen-2.5-72b", messages, CancellationToken.None);

        response.FinishReason.ShouldBe("stop");
        await innerLlm.Received(1).SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>());
    }

    // ─── Allow path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Budget_UnderLimit_Allow_ProceedsAndRecordsUsage()
    {
        var tracker = Substitute.For<ITokenTracker>();
        tracker
            .GetUsageAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new TokenUsageSummary("nem.mimir", 100, 50, 0m, 1));

        var options = Options.Create(new TokenBudgetGovernorOptions { DefaultBudget = 10_000 });
        var governor = new TokenBudgetGovernor(tracker, options);

        var innerLlm = Substitute.For<ILlmService>();
        innerLlm
            .SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("OK response", "qwen-2.5-72b", 10, 5, 15, "stop"));

        var decorator = new BudgetLlmServiceDecorator(innerLlm, governor);

        var messages = new[] { new LlmMessage("user", "Normal request") };

        var response = await decorator.SendMessageAsync("qwen-2.5-72b", messages, CancellationToken.None);

        response.FinishReason.ShouldBe("stop");
        await tracker.Received(1).RecordUsageAsync(Arg.Any<TokenUsageEvent>(), Arg.Any<CancellationToken>());
    }

    // ─── DI registration ─────────────────────────────────────────────────────

    [Fact]
    public void TokenBudgetGovernorOptions_IsConfigured_InServiceContainer()
    {
        var services = new ServiceCollection();
        services.Configure<TokenBudgetGovernorOptions>(opts => opts.DefaultBudget = 50_000);
        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<TokenBudgetGovernorOptions>>();

        options.ShouldNotBeNull();
        options.Value.DefaultBudget.ShouldBe(50_000);
    }
}
