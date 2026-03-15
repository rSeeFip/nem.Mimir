using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct AuditEntryId(Guid Value) : ITypedId<Guid>, IComparable<AuditEntryId>, IParsable<AuditEntryId>
{
    public static readonly AuditEntryId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static AuditEntryId New() => new(Guid.NewGuid());
    public static AuditEntryId From(Guid id) => new(id);

    public int CompareTo(AuditEntryId other) => Value.CompareTo(other.Value);

    public static AuditEntryId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out AuditEntryId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public static explicit operator Guid(AuditEntryId id) => id.Value;
    public static explicit operator AuditEntryId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
