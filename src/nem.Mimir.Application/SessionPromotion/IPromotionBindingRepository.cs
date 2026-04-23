using nem.Contracts.Identity;
using nem.Contracts.SessionPromotion;

namespace nem.Mimir.Application.SessionPromotion;

/// <summary>
/// Stores and retrieves promotion bindings for <see cref="ConversationForkId"/> values.
/// Used by <see cref="PromoteSessionHandler"/> to enforce first-wins conflict semantics.
/// </summary>
public interface IPromotionBindingRepository
{
    /// <summary>
    /// Returns the existing binding for <paramref name="forkId"/>, or <c>null</c> if none exists.
    /// </summary>
    Task<PromotionRecord?> GetByForkIdAsync(ConversationForkId forkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new <see cref="PromotionRecord"/> for <paramref name="forkId"/>.
    /// Implementations must raise a uniqueness violation (or equivalent) if a record
    /// for this <paramref name="forkId"/> already exists.
    /// </summary>
    Task SaveAsync(ConversationForkId forkId, PromotionRecord record, CancellationToken cancellationToken = default);
}

/// <summary>Immutable snapshot of a recorded session promotion.</summary>
public sealed record PromotionRecord(
    ChannelId OldChannelId,
    ChannelId NewChannelId,
    string AdapterName,
    DateTimeOffset PromotedAtUtc);
