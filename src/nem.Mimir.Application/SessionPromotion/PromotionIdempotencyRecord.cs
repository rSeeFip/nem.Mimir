using nem.Contracts.SessionPromotion;

namespace nem.Mimir.Application.SessionPromotion;

/// <summary>
/// Marten document that stores the result of a successful promotion for idempotency deduplication.
/// Keyed by TenantId + IdempotencyKey. Manual 24-hour TTL enforced via CreatedAt.
/// </summary>
public sealed record PromotionIdempotencyRecord
{
    /// <summary>
    /// Composite Marten document id: "{tenantId}:{idempotencyKey}".
    /// For single-tenant scenarios, tenantId is set to "default".
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>The original idempotency key provided by the caller.</summary>
    public Guid IdempotencyKey { get; init; }

    /// <summary>Tenant identifier for key isolation.</summary>
    public string TenantId { get; init; } = "default";

    /// <summary>Cached result of the first successful promotion.</summary>
    public SessionPromotedEvent CachedResult { get; init; } = default!;

    /// <summary>UTC timestamp of the first execution; used for 24-hour TTL check.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Builds the composite id used as the Marten document identity.</summary>
    public static string MakeId(string tenantId, Guid idempotencyKey)
        => $"{tenantId}:{idempotencyKey}";
}
