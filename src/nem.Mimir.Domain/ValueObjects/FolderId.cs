using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct FolderId(Guid Value) : ITypedId<Guid>, IComparable<FolderId>, IParsable<FolderId>
{
    public static readonly FolderId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static FolderId New() => new(Guid.NewGuid());
    public static FolderId From(Guid id) => new(id);

    public int CompareTo(FolderId other) => Value.CompareTo(other.Value);

    public static FolderId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out FolderId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public static explicit operator Guid(FolderId id) => id.Value;
    public static explicit operator FolderId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
