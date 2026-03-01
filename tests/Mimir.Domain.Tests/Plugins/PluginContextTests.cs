using Mimir.Domain.Plugins;
using Shouldly;

namespace Mimir.Domain.Tests.Plugins;

public class PluginContextTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_Context()
    {
        var parameters = new Dictionary<string, object> { ["input"] = "test" };

        var context = PluginContext.Create("user-123", parameters);

        context.UserId.ShouldBe("user-123");
        context.Parameters.ShouldBe(parameters);
    }

    [Fact]
    public void Create_With_Null_UserId_Should_Throw()
    {
        Should.Throw<ArgumentNullException>(() =>
            PluginContext.Create(null!, new Dictionary<string, object>()));
    }

    [Fact]
    public void Create_With_Empty_UserId_Should_Throw()
    {
        Should.Throw<ArgumentException>(() =>
            PluginContext.Create("", new Dictionary<string, object>()));
    }

    [Fact]
    public void Create_With_Null_Parameters_Should_Use_Empty_Dictionary()
    {
        var context = PluginContext.Create("user-123", null!);

        context.Parameters.ShouldNotBeNull();
        context.Parameters.ShouldBeEmpty();
    }

    [Fact]
    public void Contexts_With_Same_Values_Should_Be_Equal()
    {
        var parameters = new Dictionary<string, object> { ["key"] = "value" };
        var a = PluginContext.Create("user-123", parameters);
        var b = PluginContext.Create("user-123", parameters);

        a.ShouldBe(b);
    }
}
