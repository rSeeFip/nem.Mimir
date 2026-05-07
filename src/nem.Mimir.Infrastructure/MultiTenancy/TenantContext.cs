namespace nem.Mimir.Infrastructure.MultiTenancy;

using nem.Mimir.Domain.MultiTenancy;

public sealed class TenantContext : ITenantContext
{
    public string? TenantId { get; private set; }

    public string? TenantName { get; private set; }

    public void SetTenant(string tenantId, string? tenantName = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        }

        TenantId = tenantId;
        TenantName = string.IsNullOrWhiteSpace(tenantName) ? tenantId : tenantName;
    }
}
