namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;

public sealed class ArenaSession : BaseAuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public Guid ModelAId { get; private set; }
    public Guid ModelBId { get; private set; }
    public string ResponseA { get; private set; } = string.Empty;
    public string ResponseB { get; private set; } = string.Empty;
    public string? VotedWinner { get; private set; }
    public bool IsRevealed { get; private set; }
    public bool IsCompleted { get; private set; }
    public string? ModelAName { get; private set; }
    public string? ModelBName { get; private set; }
    public decimal? ModelAEloBefore { get; private set; }
    public decimal? ModelAEloAfter { get; private set; }
    public decimal? ModelBEloBefore { get; private set; }
    public decimal? ModelBEloAfter { get; private set; }

    private ArenaSession() { }

    public static ArenaSession Create(
        Guid userId,
        string prompt,
        Guid modelAId,
        Guid modelBId,
        string responseA,
        string responseB)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));

        if (modelAId == Guid.Empty)
            throw new ArgumentException("Model A ID cannot be empty.", nameof(modelAId));

        if (modelBId == Guid.Empty)
            throw new ArgumentException("Model B ID cannot be empty.", nameof(modelBId));

        if (modelAId == modelBId)
            throw new ArgumentException("Model A and Model B must be different models.");

        if (string.IsNullOrWhiteSpace(responseA))
            throw new ArgumentException("Response A cannot be empty.", nameof(responseA));

        if (string.IsNullOrWhiteSpace(responseB))
            throw new ArgumentException("Response B cannot be empty.", nameof(responseB));

        return new ArenaSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Prompt = prompt.Trim(),
            ModelAId = modelAId,
            ModelBId = modelBId,
            ResponseA = responseA.Trim(),
            ResponseB = responseB.Trim(),
            IsRevealed = false,
            IsCompleted = false,
        };
    }

    public void Vote(
        string winner,
        string modelAName,
        string modelBName,
        decimal modelAEloBefore,
        decimal modelAEloAfter,
        decimal modelBEloBefore,
        decimal modelBEloAfter)
    {
        if (IsCompleted)
            throw new InvalidOperationException("Arena session has already been voted.");

        if (winner is not ("A" or "B" or "Draw"))
            throw new ArgumentException("Winner must be A, B, or Draw.", nameof(winner));

        if (string.IsNullOrWhiteSpace(modelAName) || string.IsNullOrWhiteSpace(modelBName))
            throw new ArgumentException("Model names are required for reveal.");

        VotedWinner = winner;
        ModelAName = modelAName.Trim();
        ModelBName = modelBName.Trim();
        ModelAEloBefore = modelAEloBefore;
        ModelAEloAfter = modelAEloAfter;
        ModelBEloBefore = modelBEloBefore;
        ModelBEloAfter = modelBEloAfter;
        IsRevealed = true;
        IsCompleted = true;
    }
}
