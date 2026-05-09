using Microsoft.Extensions.Options;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Tokens;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Tokens;

public sealed class TokenBudgetGovernorTests
{
    private static readonly DateTimeOffset BaseTime = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CheckBudgetAsync_ReturnsAllow_WhenProjectedUsageIsUnderBudget()
    {
        var tracker = Substitute.For<ITokenTracker>();
        tracker.GetUsageAsync("tenant-a", DateTimeOffset.MinValue, BaseTime, CancellationToken.None)
            .Returns(new TokenUsageSummary("tenant-a", 200, 100, 0.02m, 1));

        var sut = CreateSut(tracker, new TokenBudgetGovernorOptions
        {
            DefaultBudget = 500,
            WarnThresholdPercent = 80.0,
        });

        var context = new TokenBudgetContext("tenant-a", "gpt-4o", 50, 25, 0.01m, BaseTime);

        var result = await sut.CheckBudgetAsync(context, CancellationToken.None);

        result.Action.ShouldBe(BudgetAction.Allow);
        result.ProjectedSpend.ShouldBe(375m);
        result.BudgetLimit.ShouldBe(500m);
        result.Reason.ShouldBeNull();
    }

    [Fact]
    public async Task CheckBudgetAsync_ReturnsWarn_WhenProjectedUsageReachesThreshold()
    {
        var tracker = Substitute.For<ITokenTracker>();
        tracker.GetUsageAsync("tenant-a", DateTimeOffset.MinValue, BaseTime, CancellationToken.None)
            .Returns(new TokenUsageSummary("tenant-a", 350, 0, 0.04m, 2));

        var sut = CreateSut(tracker, new TokenBudgetGovernorOptions
        {
            DefaultBudget = 500,
            WarnThresholdPercent = 80.0,
        });

        var context = new TokenBudgetContext("tenant-a", "gpt-4o", 50, 0, 0.01m, BaseTime);

        var result = await sut.CheckBudgetAsync(context, CancellationToken.None);

        result.Action.ShouldBe(BudgetAction.Warn);
        result.ProjectedSpend.ShouldBe(400m);
        result.BudgetLimit.ShouldBe(500m);
        result.Reason.ShouldNotBeNull();
        result.Reason.ShouldContain("warning", Case.Insensitive);
    }

    [Fact]
    public async Task CheckBudgetAsync_ReturnsDeny_WhenProjectedUsageExceedsBudget()
    {
        var tracker = Substitute.For<ITokenTracker>();
        tracker.GetUsageAsync("tenant-a", DateTimeOffset.MinValue, BaseTime, CancellationToken.None)
            .Returns(new TokenUsageSummary("tenant-a", 450, 30, 0.04m, 3));

        var sut = CreateSut(tracker, new TokenBudgetGovernorOptions
        {
            DefaultBudget = 500,
            WarnThresholdPercent = 80.0,
        });

        var context = new TokenBudgetContext("tenant-a", "gpt-4o", 30, 10, 0.01m, BaseTime);

        var result = await sut.CheckBudgetAsync(context, CancellationToken.None);

        result.Action.ShouldBe(BudgetAction.Deny);
        result.ProjectedSpend.ShouldBe(520m);
        result.BudgetLimit.ShouldBe(500m);
        result.Reason.ShouldNotBeNull();
        result.Reason.ShouldContain("exceeded", Case.Insensitive);
    }

    [Fact]
    public async Task CheckBudgetAsync_UsesTenantSpecificBudgetOverride()
    {
        var tracker = Substitute.For<ITokenTracker>();
        tracker.GetUsageAsync("tenant-a", DateTimeOffset.MinValue, BaseTime, CancellationToken.None)
            .Returns(new TokenUsageSummary("tenant-a", 450, 0, 0.04m, 2));
        tracker.GetUsageAsync("tenant-b", DateTimeOffset.MinValue, BaseTime, CancellationToken.None)
            .Returns(new TokenUsageSummary("tenant-b", 450, 0, 0.04m, 2));

        var sut = CreateSut(tracker, new TokenBudgetGovernorOptions
        {
            DefaultBudget = 500,
            WarnThresholdPercent = 80.0,
            TenantOverrides = new Dictionary<string, long>
            {
                ["tenant-b"] = 700,
            },
        });

        var tenantAResult = await sut.CheckBudgetAsync(
            new TokenBudgetContext("nem.mimir", "gpt-4o", 75, 0, 0.01m, BaseTime, "tenant-a"),
            CancellationToken.None);
        var tenantBResult = await sut.CheckBudgetAsync(
            new TokenBudgetContext("nem.mimir", "gpt-4o", 75, 0, 0.01m, BaseTime, "tenant-b"),
            CancellationToken.None);

        tenantAResult.Action.ShouldBe(BudgetAction.Deny);
        tenantBResult.Action.ShouldBe(BudgetAction.Allow);
        tenantBResult.BudgetLimit.ShouldBe(700m);
        tenantBResult.IsAllowed.ShouldBeTrue();
        tenantBResult.RemainingBudget.ShouldBe(175m);
    }

    [Fact]
    public async Task CheckBudgetAsync_UsesConfiguredWarningThresholdPercent()
    {
        var tracker = Substitute.For<ITokenTracker>();
        tracker.GetUsageAsync("tenant-a", DateTimeOffset.MinValue, BaseTime, CancellationToken.None)
            .Returns(new TokenUsageSummary("tenant-a", 300, 0, 0.04m, 2));

        var sut = CreateSut(tracker, new TokenBudgetGovernorOptions
        {
            DefaultBudget = 500,
            WarnThresholdPercent = 70.0,
        });

        var context = new TokenBudgetContext("tenant-a", "gpt-4o", 50, 0, 0.01m, BaseTime);

        var result = await sut.CheckBudgetAsync(context, CancellationToken.None);

        result.Action.ShouldBe(BudgetAction.Warn);
        result.ProjectedSpend.ShouldBe(350m);
        result.BudgetLimit.ShouldBe(500m);
    }

    [Fact]
    public async Task RecordUsageAsync_DelegatesToTokenTracker()
    {
        var tracker = Substitute.For<ITokenTracker>();
        var sut = CreateSut(tracker, new TokenBudgetGovernorOptions { DefaultBudget = 500 });
        var usage = new TokenUsage("tenant-a", "gpt-4o", 120, 40, 0.03m, BaseTime);

        await sut.RecordUsageAsync(usage, CancellationToken.None);

        await tracker.Received(1).RecordUsageAsync(
            Arg.Is<TokenUsageEvent>(x =>
                x.ServiceId == usage.ServiceId
                && x.ModelId == usage.ModelId
                && x.InputTokens == usage.InputTokens
                && x.OutputTokens == usage.OutputTokens
                && x.Cost == usage.Cost
                && x.Timestamp == usage.Timestamp),
            CancellationToken.None);
    }

    private static TokenBudgetGovernor CreateSut(ITokenTracker tracker, TokenBudgetGovernorOptions options)
    {
        return new TokenBudgetGovernor(tracker, Options.Create(options));
    }
}
