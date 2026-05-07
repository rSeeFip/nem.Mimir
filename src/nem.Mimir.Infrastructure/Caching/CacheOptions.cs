namespace nem.Mimir.Infrastructure.Caching;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public TimeSpan ModelsTtl { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan BillingTtl { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan ConfigTtl { get; init; } = TimeSpan.FromMinutes(10);
}
