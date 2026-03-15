using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct MessageId(Guid Value) : ITypedId<Guid>, IComparable<MessageId>, IParsable<MessageId>
{
    public static readonly MessageId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static MessageId New() => new(Guid.NewGuid());
    public static MessageId From(Guid id) => new(id);

    public int CompareTo(MessageId other) => Value.CompareTo(other.Value);

    public static MessageId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out MessageId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public static explicit operator Guid(MessageId id) => id.Value;
    public static explicit operator MessageId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
