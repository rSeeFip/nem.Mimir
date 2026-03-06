using Microsoft.Extensions.Logging;
using Mimir.Application.Tokens;
using nem.Contracts.TokenOptimization;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Tokens;

public sealed class TokenTrackerServiceTests
{
    private readonly ILogger<TokenTrackerService> _logger = Substitute.For<ILogger<TokenTrackerService>>();

    private static readonly DateTimeOffset BaseTime = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordUsageAsync_StoresEvent_AndCanBeQueried()
    {
        var sut = CreateSut();
        var usageEvent = new TokenUsageEvent("svc-1", "gpt-4o", 100, 200, 0.05m, BaseTime);

        await sut.RecordUsageAsync(usageEvent, CancellationToken.None);

        var summary = await sut.GetUsageAsync("svc-1", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);
        summary.ServiceId.ShouldBe("svc-1");
        summary.TotalInputTokens.ShouldBe(100);
        summary.TotalOutputTokens.ShouldBe(200);
        summary.TotalCost.ShouldBe(0.05m);
        summary.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task RecordUsageAsync_ThrowsOnNullEvent()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.RecordUsageAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task RecordUsageAsync_ThrowsOnNegativeInputTokens()
    {
        var sut = CreateSut();
        var usageEvent = new TokenUsageEvent("svc-1", "gpt-4o", -1, 200, 0.05m, BaseTime);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => sut.RecordUsageAsync(usageEvent, CancellationToken.None));
    }

    [Fact]
    public async Task RecordUsageAsync_ThrowsOnNegativeOutputTokens()
    {
        var sut = CreateSut();
        var usageEvent = new TokenUsageEvent("svc-1", "gpt-4o", 100, -5, 0.05m, BaseTime);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => sut.RecordUsageAsync(usageEvent, CancellationToken.None));
    }

    [Fact]
    public async Task RecordUsageAsync_ThrowsOnNullOrWhitespaceServiceId()
    {
        var sut = CreateSut();
        var usageEvent = new TokenUsageEvent("  ", "gpt-4o", 100, 200, 0.05m, BaseTime);

        await Should.ThrowAsync<ArgumentException>(
            () => sut.RecordUsageAsync(usageEvent, CancellationToken.None));
    }

    [Fact]
    public async Task GetUsageAsync_FiltersByDateRange()
    {
        var sut = CreateSut();

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 50, 60, 0.01m, BaseTime.AddHours(-2)), CancellationToken.None);
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 120, 0.02m, BaseTime), CancellationToken.None);
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 200, 240, 0.04m, BaseTime.AddHours(2)), CancellationToken.None);

        var summary = await sut.GetUsageAsync("svc-1", BaseTime.AddMinutes(-1), BaseTime.AddMinutes(1), CancellationToken.None);

        summary.TotalInputTokens.ShouldBe(100);
        summary.TotalOutputTokens.ShouldBe(120);
        summary.TotalCost.ShouldBe(0.02m);
        summary.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetUsageAsync_FiltersByServiceId()
    {
        var sut = CreateSut();

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 200, 0.05m, BaseTime), CancellationToken.None);
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-2", "gpt-4o", 300, 400, 0.10m, BaseTime), CancellationToken.None);

        var summary = await sut.GetUsageAsync("svc-1", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);

        summary.ServiceId.ShouldBe("svc-1");
        summary.TotalInputTokens.ShouldBe(100);
        summary.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsZeroSummary_WhenNoEventsFound()
    {
        var sut = CreateSut();

        var summary = await sut.GetUsageAsync("nonexistent", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);

        summary.ServiceId.ShouldBe("nonexistent");
        summary.TotalInputTokens.ShouldBe(0);
        summary.TotalOutputTokens.ShouldBe(0);
        summary.TotalCost.ShouldBe(0m);
        summary.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetUsageAsync_AggregatesMultipleEvents()
    {
        var sut = CreateSut();

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 200, 0.05m, BaseTime), CancellationToken.None);
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o-mini", 50, 75, 0.01m, BaseTime.AddMinutes(5)), CancellationToken.None);
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 150, 300, 0.08m, BaseTime.AddMinutes(10)), CancellationToken.None);

        var summary = await sut.GetUsageAsync("svc-1", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);

        summary.TotalInputTokens.ShouldBe(300);
        summary.TotalOutputTokens.ShouldBe(575);
        summary.TotalCost.ShouldBe(0.14m);
        summary.RequestCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetCostAsync_SumsCostsForServiceAndDateRange()
    {
        var sut = CreateSut();

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 200, 0.05m, BaseTime), CancellationToken.None);
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 150, 300, 0.08m, BaseTime.AddMinutes(5)), CancellationToken.None);
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-2", "gpt-4o", 50, 100, 0.02m, BaseTime), CancellationToken.None);

        var cost = await sut.GetCostAsync("svc-1", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);

        cost.ShouldBe(0.13m);
    }

    [Fact]
    public async Task GetCostAsync_ReturnsZero_WhenNoEventsFound()
    {
        var sut = CreateSut();

        var cost = await sut.GetCostAsync("nonexistent", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);

        cost.ShouldBe(0m);
    }

    [Fact]
    public async Task RecordUsageAsync_LogsWarning_WhenBudgetExceeded()
    {
        var options = new TokenTrackerOptions
        {
            DefaultBudgetPerService = 0.10m,
            BudgetWarningThreshold = 0.8
        };
        var sut = CreateSut(options);

        // First event brings us to 0.08 (80% of 0.10)
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 200, 0.08m, BaseTime), CancellationToken.None);
        // Second event pushes over budget (0.08 + 0.05 = 0.13 > 0.10)
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 50, 100, 0.05m, BaseTime.AddMinutes(1)), CancellationToken.None);

        // Both events should still be recorded despite budget exceeded
        var summary = await sut.GetUsageAsync("svc-1", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);
        summary.RequestCount.ShouldBe(2);
        summary.TotalCost.ShouldBe(0.13m);

        // Verify warning was logged (budget exceeded)
        _logger.ReceivedWithAnyArgs().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RecordUsageAsync_SkipsRecording_WhenTrackingDisabled()
    {
        var options = new TokenTrackerOptions { EnableTracking = false };
        var sut = CreateSut(options);

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 200, 0.05m, BaseTime), CancellationToken.None);

        var summary = await sut.GetUsageAsync("svc-1", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);
        summary.RequestCount.ShouldBe(0);
        summary.TotalCost.ShouldBe(0m);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsEmptySummary_WhenTrackingDisabled()
    {
        var options = new TokenTrackerOptions { EnableTracking = false };
        var sut = CreateSut(options);

        var summary = await sut.GetUsageAsync("svc-1", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);

        summary.ServiceId.ShouldBe("svc-1");
        summary.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetCostAsync_ReturnsZero_WhenTrackingDisabled()
    {
        var options = new TokenTrackerOptions { EnableTracking = false };
        var sut = CreateSut(options);

        var cost = await sut.GetCostAsync("svc-1", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);

        cost.ShouldBe(0m);
    }

    [Fact]
    public async Task ConcurrentAccess_RecordsAllEvents()
    {
        var sut = CreateSut();
        const int eventCount = 100;

        var tasks = Enumerable.Range(0, eventCount)
            .Select(i => sut.RecordUsageAsync(
                new TokenUsageEvent("svc-concurrent", "gpt-4o", 10, 20, 0.001m, BaseTime.AddSeconds(i)),
                CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);

        var summary = await sut.GetUsageAsync("svc-concurrent", BaseTime.AddHours(-1), BaseTime.AddHours(1), CancellationToken.None);

        summary.RequestCount.ShouldBe(eventCount);
        summary.TotalInputTokens.ShouldBe(10 * eventCount);
        summary.TotalOutputTokens.ShouldBe(20 * eventCount);
        summary.TotalCost.ShouldBe(0.001m * eventCount);
    }

    [Fact]
    public async Task RecordUsageAsync_LogsBudgetWarning_WhenThresholdReached()
    {
        var options = new TokenTrackerOptions
        {
            DefaultBudgetPerService = 1.00m,
            BudgetWarningThreshold = 0.5
        };
        var sut = CreateSut(options);

        // Record 0.60 — this puts cumulative at 0.60 which is 60% > 50% threshold
        // But we need the threshold check to trigger during recording of a second event
        // First event: 0.40 (40% of budget, below 50% threshold) — no warning
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 200, 0.40m, BaseTime), CancellationToken.None);

        // Clear any previous log calls
        _logger.ClearReceivedCalls();

        // Second event: cumulative 0.40, which is >= 50% threshold, so warning triggers before recording 0.20 more
        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 50, 100, 0.20m, BaseTime.AddMinutes(1)), CancellationToken.None);

        _logger.ReceivedWithAnyArgs().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetUsageAsync_InclusiveDateBoundaries()
    {
        var sut = CreateSut();

        await sut.RecordUsageAsync(new TokenUsageEvent("svc-1", "gpt-4o", 100, 200, 0.05m, BaseTime), CancellationToken.None);

        // Exact boundary match should be included
        var summary = await sut.GetUsageAsync("svc-1", BaseTime, BaseTime, CancellationToken.None);

        summary.RequestCount.ShouldBe(1);
        summary.TotalCost.ShouldBe(0.05m);
    }

    private TokenTrackerService CreateSut(TokenTrackerOptions? options = null)
    {
        return new TokenTrackerService(options ?? new TokenTrackerOptions(), _logger);
    }
}
