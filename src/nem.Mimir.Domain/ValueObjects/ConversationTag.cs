namespace nem.Mimir.Domain.ValueObjects;

public sealed record ConversationTag
{
    public const int MaxLength = 50;

    public string Value { get; }

    private ConversationTag(string value)
    {
        Value = value;
    }

    public static ConversationTag Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Tag cannot be empty.", nameof(value));

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
            throw new ArgumentException($"Tag cannot exceed {MaxLength} characters.", nameof(value));

        return new ConversationTag(normalized);
    }

    public override string ToString() => Value;
}
