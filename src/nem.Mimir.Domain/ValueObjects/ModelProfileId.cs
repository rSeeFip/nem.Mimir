using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct ModelProfileId(Guid Value) : ITypedId<Guid>, IComparable<ModelProfileId>, IParsable<ModelProfileId>
{
    public static readonly ModelProfileId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static ModelProfileId New() => new(Guid.NewGuid());
    public static ModelProfileId From(Guid id) => new(id);

    public int CompareTo(ModelProfileId other) => Value.CompareTo(other.Value);

    public static ModelProfileId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out ModelProfileId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public static explicit operator Guid(ModelProfileId id) => id.Value;
    public static explicit operator ModelProfileId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
