using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct EvaluationId(Guid Value) : ITypedId<Guid>, IComparable<EvaluationId>, IParsable<EvaluationId>
{
    public static readonly EvaluationId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static EvaluationId New() => new(Guid.NewGuid());
    public static EvaluationId From(Guid id) => new(id);

    public int CompareTo(EvaluationId other) => Value.CompareTo(other.Value);

    public static EvaluationId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));

    public static bool TryParse(string? s, IFormatProvider? provider, out EvaluationId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public static explicit operator Guid(EvaluationId id) => id.Value;
    public static explicit operator EvaluationId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
