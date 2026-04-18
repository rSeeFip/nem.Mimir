namespace nem.Mimir.Infrastructure.Cache;

/// <summary>
/// Configuration options for the in-memory semantic cache.
/// </summary>
public sealed class SemanticCacheOptions
{
    /// <summary>
    /// Gets the default time-to-live applied when an entry TTL is not specified.
    /// </summary>
    public TimeSpan DefaultTtl { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets the default minimum similarity required for semantic matches.
    /// Values lower than 0.90 are not allowed by the service.
    /// </summary>
    public float SimilarityThreshold { get; init; } = 0.92f;

    /// <summary>
    /// Gets the maximum number of entries allowed in the cache.
    /// </summary>
    public int MaxEntries { get; init; } = 10_000;

    /// <summary>
    /// Gets a value indicating whether cache keys are further isolated per Keycloak tenant
    /// (derived from the <c>tenant_id</c> claim). When enabled, keys from different tenants
    /// never collide even if user IDs are identical across realms.
    /// </summary>
    public bool EnableTenantIsolation { get; init; } = true;

    public bool EnablePerUserIsolation { get; init; } = true;
}
