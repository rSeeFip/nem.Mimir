namespace Mimir.Domain.ValueObjects;

public readonly record struct MessageId(Guid Value)
{
    public static MessageId New() => new(Guid.NewGuid());
    public static MessageId From(Guid id) => new(id);
    public override string ToString() => Value.ToString();
}
