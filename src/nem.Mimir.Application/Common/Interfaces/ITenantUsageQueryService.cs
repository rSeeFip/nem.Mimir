using nem.Mimir.Application.Billing;

namespace nem.Mimir.Application.Common.Interfaces;

public interface ITenantUsageQueryService
{
    Task<TenantUsageSummary> GetUsageAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
