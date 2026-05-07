namespace nem.Mimir.Infrastructure.Caching;

public static class TenantAwareCacheKey
{
    private const string TenantPrefix = "tenant";

    public static string Build(string tenantId, string key)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("tenantId must not be null or whitespace.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("key must not be null or whitespace.", nameof(key));

        return $"{TenantPrefix}:{tenantId}:{key}";
    }

    public static class Keys
    {
        public const string Models = "models";
        public const string TenantConfig = "tenant-config";
        public const string BillingSummary = "billing-summary";
    }
}
