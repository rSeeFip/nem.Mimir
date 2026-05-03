using Marten;
using nem.Mimir.Application.Billing;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Infrastructure.Billing;

public class TenantUsageQueryService : ITenantUsageQueryService
{
    private readonly IQuerySession _session;

    public TenantUsageQueryService(IQuerySession session)
    {
        _session = session;
    }

    public async Task<TenantUsageSummary> GetUsageAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var events = await _session.Query<PersistedCostEvent>()
            .Where(e => e.TenantId == tenantId
                     && e.OccurredAt >= from
                     && e.OccurredAt < to)
            .ToListAsync(cancellationToken);

        var modelBreakdown = events
            .GroupBy(e => e.Model)
            .ToDictionary(
                g => g.Key,
                g => new ModelUsage
                {
                    Model = g.Key,
                    InputTokens = g.Sum(e => (long)e.PromptTokens),
                    OutputTokens = g.Sum(e => (long)e.CompletionTokens),
                    Cost = g.Sum(e => e.CostUsd),
                    RequestCount = g.Count(),
                });

        return new TenantUsageSummary
        {
            TenantId = tenantId,
            PeriodStart = from,
            PeriodEnd = to,
            TotalInputTokens = events.Sum(e => (long)e.PromptTokens),
            TotalOutputTokens = events.Sum(e => (long)e.CompletionTokens),
            TotalCost = events.Sum(e => e.CostUsd),
            RequestCount = events.Count,
            ModelBreakdown = modelBreakdown,
        };
    }
}
