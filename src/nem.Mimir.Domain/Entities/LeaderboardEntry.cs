namespace nem.Mimir.Domain.Entities;

using LeaderboardEntryId = nem.Mimir.Domain.ValueObjects.LeaderboardEntryId;
using nem.Mimir.Domain.Common;
public sealed class LeaderboardEntry : BaseAuditableEntity<LeaderboardEntryId>
{
    public const decimal DefaultEloScore = 1500m;

    public Guid ModelId { get; private set; }
    public decimal EloScore { get; private set; }
    public int Wins { get; private set; }
    public int Losses { get; private set; }
    public int Draws { get; private set; }
    public int TotalEvaluations { get; private set; }

    private LeaderboardEntry() { }

    public static LeaderboardEntry Create(Guid modelId)
    {
        if (modelId == Guid.Empty)
            throw new ArgumentException("Model ID cannot be empty.", nameof(modelId));

        return new LeaderboardEntry
        {
            Id = LeaderboardEntryId.New(),
            ModelId = modelId,
            EloScore = DefaultEloScore,
            Wins = 0,
            Losses = 0,
            Draws = 0,
            TotalEvaluations = 0,
        };
    }

    public void ApplyOutcome(decimal actualScore, decimal newRating)
    {
        if (actualScore is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(actualScore), actualScore, "Score must be between 0 and 1.");

        if (newRating <= 0m)
            throw new ArgumentException("Elo score must be positive.", nameof(newRating));

        TotalEvaluations += 1;

        if (actualScore == 0.5m)
        {
            Draws += 1;
        }
        else if (actualScore == 1.0m)
        {
            Wins += 1;
        }
        else if (actualScore == 0.0m)
        {
            Losses += 1;
        }
        else
        {
            throw new ArgumentException("Score must be one of 0.0, 0.5, or 1.0.", nameof(actualScore));
        }

        EloScore = decimal.Round(newRating, 4, MidpointRounding.AwayFromZero);
    }
}
