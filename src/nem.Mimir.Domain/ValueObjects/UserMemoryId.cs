using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct UserMemoryId(Guid Value) : ITypedId<Guid>, IComparable<UserMemoryId>, IParsable<UserMemoryId>
{
    public static readonly UserMemoryId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static UserMemoryId New() => new(Guid.NewGuid());
    public static UserMemoryId From(Guid id) => new(id);

    public int CompareTo(UserMemoryId other) => Value.CompareTo(other.Value);

    public static UserMemoryId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out UserMemoryId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public static explicit operator Guid(UserMemoryId id) => id.Value;
    public static explicit operator UserMemoryId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
