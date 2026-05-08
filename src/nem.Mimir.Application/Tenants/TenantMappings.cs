using nem.Mimir.Application.Tenants.Dtos;
using nem.Mimir.Domain.Tenants;

namespace nem.Mimir.Application.Tenants;

internal static class TenantMappings
{
    public static TenantDto ToDto(this Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.Slug,
        tenant.Status,
        tenant.DefaultRateLimit,
        tenant.CreatedAt,
        tenant.OffboardedAt,
        tenant.DataRetentionUntil);
}
