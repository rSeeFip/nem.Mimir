using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public sealed class ArenaConfigTests
{
    [Fact]
    public void Create_WithTwoModels_ShouldCreateConfig()
    {
        var userId = Guid.NewGuid();

        var config = ArenaConfig.Create(
            userId,
            ["model-a", "model-b"],
            isBlindComparisonEnabled: true,
            showModelNamesAfterVote: false);

        config.Id.ShouldNotBe(ArenaConfigId.Empty);
        config.UserId.ShouldBe(userId);
        config.ModelIds.Count.ShouldBe(2);
        config.IsBlindComparisonEnabled.ShouldBeTrue();
        config.ShowModelNamesAfterVote.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithSingleModel_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            ArenaConfig.Create(Guid.NewGuid(), ["only-one"], true, true));
    }

    [Fact]
    public void Update_ShouldReplaceRulesAndModelIds()
    {
        var config = ArenaConfig.Create(Guid.NewGuid(), ["m1", "m2"], true, true);

        config.Update(["m2", "m3", "m4"], isBlindComparisonEnabled: false, showModelNamesAfterVote: true);

        config.ModelIds.Count.ShouldBe(3);
        config.ModelIds.ShouldContain("m3");
        config.IsBlindComparisonEnabled.ShouldBeFalse();
        config.ShowModelNamesAfterVote.ShouldBeTrue();
    }
}
