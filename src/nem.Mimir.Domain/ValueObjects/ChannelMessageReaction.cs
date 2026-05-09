namespace nem.Mimir.Domain.ValueObjects;

public sealed record ChannelMessageReaction(
    string Emoji,
    Guid UserId,
    DateTimeOffset ReactedAt)
{
    public static ChannelMessageReaction Create(string emoji, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(emoji))
        {
            throw new ArgumentException("Emoji cannot be empty.", nameof(emoji));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        return new ChannelMessageReaction(emoji.Trim(), userId, DateTimeOffset.UtcNow);
    }
}
