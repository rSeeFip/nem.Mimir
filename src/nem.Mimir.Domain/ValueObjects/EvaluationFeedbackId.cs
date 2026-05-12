using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.ValueObjects;

public readonly record struct EvaluationFeedbackId(Guid Value) : ITypedId<Guid>, IComparable<EvaluationFeedbackId>, IParsable<EvaluationFeedbackId>
{
    public static readonly EvaluationFeedbackId Empty = new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static EvaluationFeedbackId New() => new(Guid.NewGuid());
    public static EvaluationFeedbackId From(Guid id) => new(id);

    public int CompareTo(EvaluationFeedbackId other) => Value.CompareTo(other.Value);

    public static EvaluationFeedbackId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));

    public static bool TryParse(string? s, IFormatProvider? provider, out EvaluationFeedbackId result)
    {
        if (Guid.TryParse(s, out var guid))
        {
            result = new(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public static explicit operator Guid(EvaluationFeedbackId id) => id.Value;
    public static explicit operator EvaluationFeedbackId(Guid id) => new(id);

    public override string ToString() => Value.ToString();
}
