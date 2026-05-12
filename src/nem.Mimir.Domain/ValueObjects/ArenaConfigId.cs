using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct ArenaConfigId(Guid Value) : ITypedId<Guid>, IComparable<ArenaConfigId>, IParsable<ArenaConfigId>
{
    public static readonly ArenaConfigId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static ArenaConfigId New() => new(Guid.NewGuid());
    public static ArenaConfigId From(Guid id) => new(id);

    public int CompareTo(ArenaConfigId other) => Value.CompareTo(other.Value);

    public static ArenaConfigId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out ArenaConfigId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public static explicit operator Guid(ArenaConfigId id) => id.Value;
    public static explicit operator ArenaConfigId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
