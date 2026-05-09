using nem.Mimir.Domain.Plugins;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Plugins;

public class PluginStatusTests
{
    [Fact]
    public void PluginStatus_Should_Have_Expected_Values()
    {
        // Assert all expected enum values exist
        Enum.IsDefined(typeof(PluginStatus), PluginStatus.Unloaded).ShouldBeTrue();
        Enum.IsDefined(typeof(PluginStatus), PluginStatus.Loaded).ShouldBeTrue();
        Enum.IsDefined(typeof(PluginStatus), PluginStatus.Running).ShouldBeTrue();
        Enum.IsDefined(typeof(PluginStatus), PluginStatus.Error).ShouldBeTrue();
    }

    [Fact]
    public void PluginStatus_Unloaded_Should_Be_Default()
    {
        default(PluginStatus).ShouldBe(PluginStatus.Unloaded);
    }

    [Fact]
    public void PluginStatus_Should_Have_Four_Values()
    {
        var values = Enum.GetValues<PluginStatus>();
        values.Length.ShouldBe(4);
    }
}
