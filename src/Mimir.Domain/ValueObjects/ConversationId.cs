namespace Mimir.Domain.ValueObjects;

public readonly record struct ConversationId(Guid Value)
{
    public static ConversationId New() => new(Guid.NewGuid());
    public static ConversationId From(Guid id) => new(id);
    public override string ToString() => Value.ToString();
}
