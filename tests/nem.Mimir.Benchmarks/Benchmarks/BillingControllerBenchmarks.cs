using BenchmarkDotNet.Attributes;

namespace nem.Mimir.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class BillingControllerBenchmarks
{
    private static readonly DateTimeOffset From = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private BenchmarkBillingController _controller = null!;

    [GlobalSetup]
    public void Setup()
    {
        _controller = new BenchmarkBillingController(new FakeTenantUsageQueryService(), new FakeCurrentUserService("tenant-abc"));
    }

    [Benchmark(Baseline = true)]
    public Task<TenantUsageSnapshot> GetUsage() => _controller.GetUsage(From, To);

    [Benchmark]
    public Task<Dictionary<string, ModelUsageSnapshot>> GetUsageByModel() => _controller.GetUsageByModel(From, To);

    private sealed class FakeCurrentUserService(string? userId)
    {
        public string? UserId => userId;
    }

    private sealed class BenchmarkBillingController(FakeTenantUsageQueryService usageQueryService, FakeCurrentUserService currentUserService)
    {
        public Task<TenantUsageSnapshot> GetUsage(DateTimeOffset from, DateTimeOffset to)
        {
            var tenantId = currentUserService.UserId ?? string.Empty;
            return usageQueryService.GetUsageAsync(tenantId, from, to);
        }

        public async Task<Dictionary<string, ModelUsageSnapshot>> GetUsageByModel(DateTimeOffset from, DateTimeOffset to)
        {
            var tenantId = currentUserService.UserId ?? string.Empty;
            var summary = await usageQueryService.GetUsageAsync(tenantId, from, to).ConfigureAwait(false);
            return summary.ModelBreakdown;
        }
    }

    private sealed class FakeTenantUsageQueryService
    {
        private static readonly TenantUsageSnapshot Summary = new()
        {
            TenantId = "tenant-abc",
            PeriodStart = From,
            PeriodEnd = To,
            TotalInputTokens = 120_000,
            TotalOutputTokens = 65_000,
            TotalCost = 18.42m,
            RequestCount = 480,
            ModelBreakdown = new Dictionary<string, ModelUsageSnapshot>
            {
                ["gpt-4o"] = new() { Model = "gpt-4o", InputTokens = 90_000, OutputTokens = 45_000, Cost = 14.1m, RequestCount = 300 },
                ["gpt-4o-mini"] = new() { Model = "gpt-4o-mini", InputTokens = 30_000, OutputTokens = 20_000, Cost = 4.32m, RequestCount = 180 },
            },
        };

        public Task<TenantUsageSnapshot> GetUsageAsync(string tenantId, DateTimeOffset from, DateTimeOffset to)
        {
            return Task.FromResult(new TenantUsageSnapshot
            {
                TenantId = tenantId,
                PeriodStart = from,
                PeriodEnd = to,
                TotalInputTokens = Summary.TotalInputTokens,
                TotalOutputTokens = Summary.TotalOutputTokens,
                TotalCost = Summary.TotalCost,
                RequestCount = Summary.RequestCount,
                ModelBreakdown = Summary.ModelBreakdown.ToDictionary(
                    pair => pair.Key,
                    pair => new ModelUsageSnapshot
                    {
                        Model = pair.Value.Model,
                        InputTokens = pair.Value.InputTokens,
                        OutputTokens = pair.Value.OutputTokens,
                        Cost = pair.Value.Cost,
                        RequestCount = pair.Value.RequestCount,
                    }),
            });
        }
    }

    public sealed class TenantUsageSnapshot
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTimeOffset PeriodStart { get; set; }
        public DateTimeOffset PeriodEnd { get; set; }
        public long TotalInputTokens { get; set; }
        public long TotalOutputTokens { get; set; }
        public decimal TotalCost { get; set; }
        public int RequestCount { get; set; }
        public Dictionary<string, ModelUsageSnapshot> ModelBreakdown { get; set; } = new();
    }

    public sealed class ModelUsageSnapshot
    {
        public string Model { get; set; } = string.Empty;
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public decimal Cost { get; set; }
        public int RequestCount { get; set; }
    }
}
