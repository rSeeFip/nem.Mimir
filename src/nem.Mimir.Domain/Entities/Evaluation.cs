namespace nem.Mimir.Domain.Entities;

using EvaluationId = nem.Mimir.Domain.ValueObjects.EvaluationId;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;

public sealed class Evaluation : BaseAuditableEntity<EvaluationId>
{
    public Guid ModelAId { get; private set; }
    public Guid ModelBId { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public string ResponseA { get; private set; } = string.Empty;
    public string ResponseB { get; private set; } = string.Empty;
    public EvaluationWinner Winner { get; private set; }
    public EvaluationStatus Status { get; private set; }
    public Guid UserId { get; private set; }

    private Evaluation() { }

    public static Evaluation Create(
        Guid userId,
        Guid modelAId,
        Guid modelBId,
        string prompt,
        string responseA,
        string responseB)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (modelAId == Guid.Empty)
            throw new ArgumentException("Model A ID cannot be empty.", nameof(modelAId));

        if (modelBId == Guid.Empty)
            throw new ArgumentException("Model B ID cannot be empty.", nameof(modelBId));

        if (modelAId == modelBId)
            throw new ArgumentException("Model A and Model B must be different models.");

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

        if (string.IsNullOrWhiteSpace(responseA))
            throw new ArgumentException("Response A cannot be empty.", nameof(responseA));

        if (string.IsNullOrWhiteSpace(responseB))
            throw new ArgumentException("Response B cannot be empty.", nameof(responseB));

        return new Evaluation
        {
            Id = EvaluationId.New(),
            UserId = userId,
            ModelAId = modelAId,
            ModelBId = modelBId,
            Prompt = prompt.Trim(),
            ResponseA = responseA.Trim(),
            ResponseB = responseB.Trim(),
            Winner = EvaluationWinner.None,
            Status = EvaluationStatus.Pending,
        };
    }

    public void SubmitResult(EvaluationWinner winner)
    {
        if (Status == EvaluationStatus.Completed)
            throw new InvalidOperationException("Evaluation has already been completed.");

        if (winner == EvaluationWinner.None)
            throw new ArgumentException("Winner must be ModelA, ModelB, or Draw.", nameof(winner));

        Winner = winner;
        Status = EvaluationStatus.Completed;
    }
}
