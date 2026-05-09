using nem.Mimir.Domain.Entities;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public sealed class ArenaSessionTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_ArenaSession()
    {
        var session = ArenaSession.Create(
            Guid.NewGuid(),
            "Compare two responses",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Response A",
            "Response B");

        session.Prompt.ShouldBe("Compare two responses");
        session.IsRevealed.ShouldBeFalse();
        session.IsCompleted.ShouldBeFalse();
        session.VotedWinner.ShouldBeNull();
    }

    [Fact]
    public void Vote_Should_Reveal_Models_And_Set_Elo_Changes()
    {
        var session = ArenaSession.Create(
            Guid.NewGuid(),
            "Prompt",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A",
            "B");

        session.Vote("A", "Model One", "Model Two", 1500m, 1516m, 1500m, 1484m);

        session.IsCompleted.ShouldBeTrue();
        session.IsRevealed.ShouldBeTrue();
        session.VotedWinner.ShouldBe("A");
        session.ModelAName.ShouldBe("Model One");
        session.ModelBName.ShouldBe("Model Two");
        session.ModelAEloBefore.ShouldBe(1500m);
        session.ModelAEloAfter.ShouldBe(1516m);
        session.ModelBEloBefore.ShouldBe(1500m);
        session.ModelBEloAfter.ShouldBe(1484m);
    }

    [Fact]
    public void Vote_When_Already_Completed_Should_Throw()
    {
        var session = ArenaSession.Create(
            Guid.NewGuid(),
            "Prompt",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A",
            "B");
        session.Vote("Draw", "Model One", "Model Two", 1500m, 1500m, 1500m, 1500m);

        Should.Throw<InvalidOperationException>(() =>
            session.Vote("A", "Model One", "Model Two", 1500m, 1510m, 1500m, 1490m));
    }
}
