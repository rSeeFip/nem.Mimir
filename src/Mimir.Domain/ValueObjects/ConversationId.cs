using Mimir.Domain.Common;

namespace Mimir.Domain.ValueObjects;

public readonly record struct ConversationId(Guid Value) : ITypedId<Guid>, IComparable<ConversationId>, IParsable<ConversationId>
{
    public static readonly ConversationId Empty = new(Guid.Empty);
    
    public bool IsEmpty => Value == Guid.Empty;
    
    public static ConversationId New() => new(Guid.NewGuid());
    public static ConversationId From(Guid id) => new(id);
    
    public int CompareTo(ConversationId other) => Value.CompareTo(other.Value);
    
    public static ConversationId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));
    public static bool TryParse(string? s, IFormatProvider? provider, out ConversationId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }
        result = Empty;
        return false;
    }
    
    public static explicit operator Guid(ConversationId id) => id.Value;
    public static explicit operator ConversationId(Guid id) => new(id);
    
    public override string ToString() => Value.ToString();
}
