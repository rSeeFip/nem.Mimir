using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public sealed class EvaluationTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_Evaluation()
    {
        var evaluation = Evaluation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Explain eventual consistency",
            "Model A answer",
            "Model B answer");

        evaluation.Status.ShouldBe(EvaluationStatus.Pending);
        evaluation.Winner.ShouldBe(EvaluationWinner.None);
        evaluation.Prompt.ShouldBe("Explain eventual consistency");
    }

    [Fact]
    public void SubmitResult_Should_Set_Winner_And_Status_Completed()
    {
        var evaluation = Evaluation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Prompt",
            "A",
            "B");

        evaluation.SubmitResult(EvaluationWinner.ModelA);

        evaluation.Winner.ShouldBe(EvaluationWinner.ModelA);
        evaluation.Status.ShouldBe(EvaluationStatus.Completed);
    }

    [Fact]
    public void SubmitResult_When_Already_Completed_Should_Throw()
    {
        var evaluation = Evaluation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Prompt",
            "A",
            "B");
        evaluation.SubmitResult(EvaluationWinner.Draw);

        Should.Throw<InvalidOperationException>(() => evaluation.SubmitResult(EvaluationWinner.ModelB));
    }

    [Fact]
    public void LeaderboardEntry_ApplyOutcome_Should_Update_Counters_And_Rating()
    {
        var entry = LeaderboardEntry.Create(Guid.NewGuid());

        entry.ApplyOutcome(1.0m, 1516.12345m);
        entry.ApplyOutcome(0.5m, 1512.5000m);
        entry.ApplyOutcome(0.0m, 1499.98765m);

        entry.Wins.ShouldBe(1);
        entry.Draws.ShouldBe(1);
        entry.Losses.ShouldBe(1);
        entry.TotalEvaluations.ShouldBe(3);
        entry.EloScore.ShouldBe(1499.9877m);
    }

    [Fact]
    public void EvaluationFeedback_Create_Should_Calculate_Average_Score()
    {
        var feedback = EvaluationFeedback.Create(
            Domain.ValueObjects.EvaluationId.New(),
            Guid.NewGuid(),
            5,
            4,
            3,
            "Useful evaluation");

        feedback.Score.ShouldBe(4.0000m);
        feedback.Quality.ShouldBe(5);
        feedback.Relevance.ShouldBe(4);
        feedback.Accuracy.ShouldBe(3);
    }
}
