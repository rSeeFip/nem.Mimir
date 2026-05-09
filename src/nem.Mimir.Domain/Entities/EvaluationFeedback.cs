namespace nem.Mimir.Domain.Entities;

using EvaluationFeedbackId = nem.Mimir.Domain.ValueObjects.EvaluationFeedbackId;
using EvaluationId = nem.Mimir.Domain.ValueObjects.EvaluationId;
using nem.Mimir.Domain.Common;

public sealed class EvaluationFeedback : BaseAuditableEntity<EvaluationFeedbackId>
{
    public EvaluationId EvaluationId { get; private set; }
    public Guid UserId { get; private set; }
    public int Quality { get; private set; }
    public int Relevance { get; private set; }
    public int Accuracy { get; private set; }
    public decimal Score { get; private set; }
    public string? Comment { get; private set; }

    private EvaluationFeedback() { }

    public static EvaluationFeedback Create(
        EvaluationId evaluationId,
        Guid userId,
        int quality,
        int relevance,
        int accuracy,
        string? comment)
    {
        if (evaluationId.IsEmpty)
            throw new ArgumentException("Evaluation ID cannot be empty.", nameof(evaluationId));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        ValidateRating(nameof(quality), quality);
        ValidateRating(nameof(relevance), relevance);
        ValidateRating(nameof(accuracy), accuracy);

        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (!string.IsNullOrEmpty(normalizedComment) && normalizedComment.Length > 2000)
            throw new ArgumentException("Comment must not exceed 2000 characters.", nameof(comment));

        var score = decimal.Round((quality + relevance + accuracy) / 3m, 4, MidpointRounding.AwayFromZero);

        return new EvaluationFeedback
        {
            Id = EvaluationFeedbackId.New(),
            EvaluationId = evaluationId,
            UserId = userId,
            Quality = quality,
            Relevance = relevance,
            Accuracy = accuracy,
            Score = score,
            Comment = normalizedComment,
        };
    }

    private static void ValidateRating(string fieldName, int value)
    {
        if (value is < 1 or > 5)
            throw new ArgumentOutOfRangeException(fieldName, value, "Rating must be between 1 and 5.");
    }
}
