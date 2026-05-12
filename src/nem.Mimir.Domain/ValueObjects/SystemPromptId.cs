using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct SystemPromptId(Guid Value) : ITypedId<Guid>, IComparable<SystemPromptId>, IParsable<SystemPromptId>
{
    public static readonly SystemPromptId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static SystemPromptId New() => new(Guid.NewGuid());
    public static SystemPromptId From(Guid id) => new(id);

    public int CompareTo(SystemPromptId other) => Value.CompareTo(other.Value);

    public static SystemPromptId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out SystemPromptId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public static explicit operator Guid(SystemPromptId id) => id.Value;
    public static explicit operator SystemPromptId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
