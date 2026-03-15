using nem.Mimir.Domain.Plugins;
using Shouldly;

namespace nem.Mimir.Domain.Tests.Plugins;

public class PluginResultTests
{
    [Fact]
    public void Success_Should_Create_Successful_Result()
    {
        var data = new Dictionary<string, object> { ["key"] = "value" };

        var result = PluginResult.Success(data);

        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldBe(data);
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Success_Without_Data_Should_Create_Successful_Result_With_Empty_Data()
    {
        var result = PluginResult.Success();

        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.ShouldBeEmpty();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Failure_Should_Create_Failed_Result()
    {
        var result = PluginResult.Failure("Something went wrong");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Something went wrong");
        result.Data.ShouldNotBeNull();
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public void Failure_With_Null_Message_Should_Throw()
    {
        Should.Throw<ArgumentNullException>(() => PluginResult.Failure(null!));
    }

    [Fact]
    public void Failure_With_Empty_Message_Should_Throw()
    {
        Should.Throw<ArgumentException>(() => PluginResult.Failure(""));
    }
}
