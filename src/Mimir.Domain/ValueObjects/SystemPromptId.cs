namespace Mimir.Domain.ValueObjects;

public readonly record struct SystemPromptId(Guid Value)
{
    public static SystemPromptId New() => new(Guid.NewGuid());
    public static SystemPromptId From(Guid id) => new(id);
    public override string ToString() => Value.ToString();
}
