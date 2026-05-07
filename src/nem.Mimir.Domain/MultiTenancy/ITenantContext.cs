namespace nem.Mimir.Domain.MultiTenancy;

public interface ITenantContext
{
    string? TenantId { get; }

    string? TenantName { get; }
}
