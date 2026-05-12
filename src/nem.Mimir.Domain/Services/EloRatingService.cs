namespace nem.Mimir.Domain.Services;

public static class EloRatingService
{
    private const decimal NewModelKFactor = 32m;
    private const decimal EstablishedModelKFactor = 16m;

    public static decimal CalculateExpectedScore(decimal ratingA, decimal ratingB)
    {
        var exponent = (double)((ratingB - ratingA) / 400m);
        var denominator = 1d + Math.Pow(10d, exponent);
        return decimal.Round((decimal)(1d / denominator), 10, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateUpdatedRating(decimal currentRating, decimal expectedScore, decimal actualScore, int totalGames)
    {
        var kFactor = totalGames < 30 ? NewModelKFactor : EstablishedModelKFactor;
        var updated = currentRating + (kFactor * (actualScore - expectedScore));
        return decimal.Round(updated, 4, MidpointRounding.AwayFromZero);
    }

    public static (decimal modelANewRating, decimal modelBNewRating) CalculateMatchResult(
        decimal modelARating,
        decimal modelBRating,
        decimal modelAScore,
        decimal modelBScore,
        int modelATotalGames,
        int modelBTotalGames)
    {
        var expectedA = CalculateExpectedScore(modelARating, modelBRating);
        var expectedB = CalculateExpectedScore(modelBRating, modelARating);

        var updatedA = CalculateUpdatedRating(modelARating, expectedA, modelAScore, modelATotalGames);
        var updatedB = CalculateUpdatedRating(modelBRating, expectedB, modelBScore, modelBTotalGames);

        return (updatedA, updatedB);
    }
}
