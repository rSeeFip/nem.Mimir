namespace nem.Mimir.Domain.ValueObjects;

public sealed record MessageReaction(string Emoji, Guid UserId, DateTimeOffset ReactedAt)
{
    public static MessageReaction Create(string emoji, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            throw new ArgumentException("Emoji is required.", nameof(emoji));

        if (emoji.Length > 32)
            throw new ArgumentException("Emoji must not exceed 32 characters.", nameof(emoji));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        return new MessageReaction(emoji.Trim(), userId, DateTimeOffset.UtcNow);
    }
}
