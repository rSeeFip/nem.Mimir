using nem.Mimir.Domain.Services;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Services;

public sealed class EloRatingServiceTests
{
    [Fact]
    public void CalculateExpectedScore_ForEqualRatings_ShouldBeHalf()
    {
        var expected = EloRatingService.CalculateExpectedScore(1500m, 1500m);

        expected.ShouldBe(0.5000000000m);
    }

    [Fact]
    public void CalculateUpdatedRating_ForNewModelWin_ShouldApplyK32()
    {
        var expected = EloRatingService.CalculateExpectedScore(1500m, 1500m);
        var updated = EloRatingService.CalculateUpdatedRating(1500m, expected, 1.0m, totalGames: 0);

        updated.ShouldBe(1516.0000m);
    }

    [Fact]
    public void CalculateUpdatedRating_ForEstablishedModelWin_ShouldApplyK16()
    {
        var expected = EloRatingService.CalculateExpectedScore(1500m, 1500m);
        var updated = EloRatingService.CalculateUpdatedRating(1500m, expected, 1.0m, totalGames: 30);

        updated.ShouldBe(1508.0000m);
    }

    [Fact]
    public void CalculateMatchResult_Draw_ShouldAdjustBothRatingsTowardEachOther()
    {
        var (ratingA, ratingB) = EloRatingService.CalculateMatchResult(
            modelARating: 1600m,
            modelBRating: 1400m,
            modelAScore: 0.5m,
            modelBScore: 0.5m,
            modelATotalGames: 40,
            modelBTotalGames: 40);

        ratingA.ShouldBeLessThan(1600m);
        ratingB.ShouldBeGreaterThan(1400m);
    }
}
