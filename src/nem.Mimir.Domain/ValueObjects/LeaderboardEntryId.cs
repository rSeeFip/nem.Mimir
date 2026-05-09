using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct LeaderboardEntryId(Guid Value) : ITypedId<Guid>, IComparable<LeaderboardEntryId>, IParsable<LeaderboardEntryId>
{
    public static readonly LeaderboardEntryId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static LeaderboardEntryId New() => new(Guid.NewGuid());
    public static LeaderboardEntryId From(Guid id) => new(id);

    public int CompareTo(LeaderboardEntryId other) => Value.CompareTo(other.Value);

    public static LeaderboardEntryId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));

    public static bool TryParse(string? s, IFormatProvider? provider, out LeaderboardEntryId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public static explicit operator Guid(LeaderboardEntryId id) => id.Value;
    public static explicit operator LeaderboardEntryId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
