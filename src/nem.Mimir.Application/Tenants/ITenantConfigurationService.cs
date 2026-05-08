using nem.Mimir.Domain.Tenants;

namespace nem.Mimir.Application.Tenants;

public interface ITenantConfigurationService
{
    Task<TenantConfiguration?> GetAsync(string tenantId, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantConfiguration configuration, CancellationToken cancellationToken = default);
}
