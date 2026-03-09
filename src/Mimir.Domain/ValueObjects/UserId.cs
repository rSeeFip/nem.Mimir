using Mimir.Domain.Common;

namespace Mimir.Domain.ValueObjects;

public readonly record struct UserId(Guid Value) : ITypedId<Guid>, IComparable<UserId>, IParsable<UserId>
{
    public static readonly UserId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(Guid id) => new(id);

    public int CompareTo(UserId other) => Value.CompareTo(other.Value);

    public static UserId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out UserId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public static explicit operator Guid(UserId id) => id.Value;
    public static explicit operator UserId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
