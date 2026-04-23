using nem.Contracts.SessionPromotion;

namespace nem.Mimir.Application.Common.Exceptions;

public sealed class PromotionIdempotencyConflictException : ConflictException
{
    public SessionPromotedEvent CachedResult { get; }

    public Guid IdempotencyKey { get; }

    public PromotionIdempotencyConflictException(Guid idempotencyKey, SessionPromotedEvent cachedResult)
        : base($"Promotion with IdempotencyKey '{idempotencyKey}' was already processed.")
    {
        IdempotencyKey = idempotencyKey;
        CachedResult = cachedResult;
    }
}
