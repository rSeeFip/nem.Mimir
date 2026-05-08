namespace nem.Mimir.Domain.Tenants;

/// <summary>
/// Represents an onboarded tenant in the platform.
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public int DefaultRateLimit { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? OffboardedAt { get; set; }

    /// <summary>
    /// Data is retained during the offboarding window and must not be hard deleted before this timestamp.
    /// </summary>
    public DateTimeOffset? DataRetentionUntil { get; set; }

    public void Offboard(DateTimeOffset utcNow, TimeSpan retentionPeriod)
    {
        Status = TenantStatus.Offboarded;
        OffboardedAt = utcNow;
        DataRetentionUntil = utcNow.Add(retentionPeriod);
    }
}

public enum TenantStatus
{
    Active = 0,
    Suspended = 1,
    Offboarded = 2,
}
