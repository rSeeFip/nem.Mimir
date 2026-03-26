using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct KnowledgeCollectionId(Guid Value) : ITypedId<Guid>, IComparable<KnowledgeCollectionId>, IParsable<KnowledgeCollectionId>
{
    public static readonly KnowledgeCollectionId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static KnowledgeCollectionId New() => new(Guid.NewGuid());
    public static KnowledgeCollectionId From(Guid id) => new(id);

    public int CompareTo(KnowledgeCollectionId other) => Value.CompareTo(other.Value);

    public static KnowledgeCollectionId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out KnowledgeCollectionId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public static explicit operator Guid(KnowledgeCollectionId id) => id.Value;
    public static explicit operator KnowledgeCollectionId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
