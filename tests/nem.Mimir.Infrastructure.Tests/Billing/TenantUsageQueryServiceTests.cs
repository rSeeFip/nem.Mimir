using Marten;
using nem.Mimir.Infrastructure.Billing;
using Shouldly;
using Testcontainers.PostgreSql;

namespace nem.Mimir.Infrastructure.Tests.Billing;

public class TenantUsageQueryServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithImage("postgres:16-alpine")
        .Build();

    private IDocumentStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.Schema.For<PersistedCostEvent>()
                .UniqueIndex(x => x.IdempotencyKey);
            opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.CreateOrUpdate;
        });
    }

    public async ValueTask DisposeAsync()
    {
        _store.Dispose();
        await _postgres.DisposeAsync();
    }

    private static PersistedCostEvent MakeEvent(
        string tenantId,
        string model,
        int promptTokens,
        int completionTokens,
        decimal cost,
        DateTimeOffset occurredAt,
        string? userId = null)
    {
        var uid = userId ?? "user-1";
        return new PersistedCostEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = uid,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
            CostUsd = cost,
            Channel = "api",
            OccurredAt = occurredAt,
            IdempotencyKey = PersistedCostEvent.ComputeIdempotencyKey(tenantId, uid, model, occurredAt),
        };
    }

    [Fact]
    public async Task GetUsageAsync_EmptyStore_ReturnsZeroSummary()
    {
        await using var session = _store.QuerySession();
        var sut = new TenantUsageQueryService(session);

        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.GetUsageAsync("tenant-x", from, to);

        result.TenantId.ShouldBe("tenant-x");
        result.PeriodStart.ShouldBe(from);
        result.PeriodEnd.ShouldBe(to);
        result.TotalInputTokens.ShouldBe(0L);
        result.TotalOutputTokens.ShouldBe(0L);
        result.TotalCost.ShouldBe(0m);
        result.RequestCount.ShouldBe(0);
        result.ModelBreakdown.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUsageAsync_AggregatesTokensAndCostCorrectly()
    {
        var t = "tenant-agg";
        var at1 = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var at2 = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        await using (var session = _store.LightweightSession())
        {
            session.Store(MakeEvent(t, "gpt-4o", 100, 200, 0.05m, at1));
            session.Store(MakeEvent(t, "gpt-4o", 150, 250, 0.07m, at2, "user-2"));
            await session.SaveChangesAsync();
        }

        await using var qs = _store.QuerySession();
        var sut = new TenantUsageQueryService(qs);

        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.GetUsageAsync(t, from, to);

        result.RequestCount.ShouldBe(2);
        result.TotalInputTokens.ShouldBe(250L);
        result.TotalOutputTokens.ShouldBe(450L);
        result.TotalCost.ShouldBe(0.12m);
        result.ModelBreakdown.ShouldContainKey("gpt-4o");
        result.ModelBreakdown["gpt-4o"].RequestCount.ShouldBe(2);
        result.ModelBreakdown["gpt-4o"].InputTokens.ShouldBe(250L);
        result.ModelBreakdown["gpt-4o"].OutputTokens.ShouldBe(450L);
        result.ModelBreakdown["gpt-4o"].Cost.ShouldBe(0.12m);
    }

    [Fact]
    public async Task GetUsageAsync_GroupsByModelCorrectly()
    {
        var t = "tenant-models";
        var at = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

        await using (var session = _store.LightweightSession())
        {
            session.Store(MakeEvent(t, "gpt-4o", 100, 200, 0.05m, at));
            session.Store(MakeEvent(t, "gpt-4o-mini", 50, 100, 0.01m, at, "user-2"));
            await session.SaveChangesAsync();
        }

        await using var qs = _store.QuerySession();
        var sut = new TenantUsageQueryService(qs);

        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.GetUsageAsync(t, from, to);

        result.RequestCount.ShouldBe(2);
        result.ModelBreakdown.Count.ShouldBe(2);
        result.ModelBreakdown.ShouldContainKey("gpt-4o");
        result.ModelBreakdown.ShouldContainKey("gpt-4o-mini");
        result.ModelBreakdown["gpt-4o"].RequestCount.ShouldBe(1);
        result.ModelBreakdown["gpt-4o-mini"].RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetUsageAsync_DateRangeFiltering_ExcludesOutOfRangeEvents()
    {
        var t = "tenant-datefilter";
        var inRange = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var beforeRange = new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var atEnd = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        await using (var session = _store.LightweightSession())
        {
            session.Store(MakeEvent(t, "gpt-4o", 100, 200, 0.05m, inRange));
            session.Store(MakeEvent(t, "gpt-4o", 50, 100, 0.02m, beforeRange, "user-2"));
            session.Store(MakeEvent(t, "gpt-4o", 75, 150, 0.03m, atEnd, "user-3"));
            await session.SaveChangesAsync();
        }

        await using var qs = _store.QuerySession();
        var sut = new TenantUsageQueryService(qs);

        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.GetUsageAsync(t, from, to);

        result.RequestCount.ShouldBe(1);
        result.TotalInputTokens.ShouldBe(100L);
    }

    [Fact]
    public async Task GetUsageAsync_IsolatesByTenantId()
    {
        var at = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

        await using (var session = _store.LightweightSession())
        {
            session.Store(MakeEvent("tenant-A", "gpt-4o", 100, 200, 0.05m, at));
            session.Store(MakeEvent("tenant-B", "gpt-4o", 999, 999, 9.99m, at, "user-2"));
            await session.SaveChangesAsync();
        }

        await using var qs = _store.QuerySession();
        var sut = new TenantUsageQueryService(qs);

        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.GetUsageAsync("tenant-A", from, to);

        result.RequestCount.ShouldBe(1);
        result.TotalCost.ShouldBe(0.05m);
    }
}
