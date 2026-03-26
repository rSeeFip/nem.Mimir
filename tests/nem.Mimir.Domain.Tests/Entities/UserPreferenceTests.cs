using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Entities;

public class UserPreferenceTests
{
    [Fact]
    public void Create_WithValidUserId_ShouldCreatePreferenceWithDefaults()
    {
        var userId = Guid.NewGuid();

        var preference = UserPreference.Create(userId);

        preference.Id.ShouldNotBe(UserPreferenceId.Empty);
        preference.UserId.ShouldBe(userId);
        preference.Settings.Keys.ShouldContain("general");
        preference.Settings.Keys.ShouldContain("chat");
        preference.Settings.Keys.ShouldContain("audio");
        preference.Settings.Keys.ShouldContain("notifications");
        preference.Settings.Keys.ShouldContain("appearance");
        preference.Settings.Keys.ShouldContain("interface");
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => UserPreference.Create(Guid.Empty));
    }

    [Fact]
    public void UpdateSection_WithValidSection_ShouldUpdateValues()
    {
        var preference = UserPreference.Create(Guid.NewGuid());

        preference.UpdateSection("general", new Dictionary<string, object>
        {
            ["language"] = "de",
            ["timezone"] = "Europe/Berlin",
        });

        preference.Settings["general"]["language"].ShouldBe("de");
        preference.Settings["general"]["timezone"].ShouldBe("Europe/Berlin");
    }

    [Fact]
    public void UpdateSection_WithUnknownSection_ShouldThrow()
    {
        var preference = UserPreference.Create(Guid.NewGuid());

        Should.Throw<ArgumentException>(() => preference.UpdateSection("unknown", new Dictionary<string, object>()));
    }

    [Fact]
    public void ResetToDefaults_ShouldRestoreDefaultValues()
    {
        var preference = UserPreference.Create(Guid.NewGuid());
        preference.UpdateSection("general", new Dictionary<string, object> { ["language"] = "fr" });

        preference.ResetToDefaults();

        preference.Settings["general"]["language"].ShouldBe("en");
    }
}
