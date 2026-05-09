using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct UserPreferenceId(Guid Value) : ITypedId<Guid>, IComparable<UserPreferenceId>, IParsable<UserPreferenceId>
{
    public static readonly UserPreferenceId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static UserPreferenceId New() => new(Guid.NewGuid());
    public static UserPreferenceId From(Guid id) => new(id);

    public int CompareTo(UserPreferenceId other) => Value.CompareTo(other.Value);

    public static UserPreferenceId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out UserPreferenceId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public static explicit operator Guid(UserPreferenceId id) => id.Value;
    public static explicit operator UserPreferenceId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
