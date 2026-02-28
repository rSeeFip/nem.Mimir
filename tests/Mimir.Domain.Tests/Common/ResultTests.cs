using Mimir.Domain.Common;
using Shouldly;
using Xunit;

namespace Mimir.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Result_Success_Creates_Success_Result()
    {
        // Arrange & Act
        var result = Result<string>.Success("test value");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBe("test value");
    }

    [Fact]
    public void Result_Failure_Creates_Failure_Result()
    {
        // Arrange & Act
        var result = Result<string>.Failure("error message");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("error message");
    }

    [Fact]
    public void Result_Failure_Accessing_Value_Throws_Exception()
    {
        // Arrange
        var result = Result<string>.Failure("error message");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Result_Success_Accessing_Error_Throws_Exception()
    {
        // Arrange
        var result = Result<string>.Success("test");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _ = result.Error);
    }

    [Fact]
    public void Result_Implicit_Conversion_From_Value()
    {
        // Arrange & Act
        Result<string> result = "test value";

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("test value");
    }

    [Fact]
    public void Result_Supports_Different_Types()
    {
        // Arrange & Act
        var intResult = Result<int>.Success(42);
        var boolResult = Result<bool>.Success(true);

        // Assert
        intResult.Value.ShouldBe(42);
        boolResult.Value.ShouldBeTrue();
    }

    [Fact]
    public void Result_With_Null_Error_Message()
    {
        // Arrange & Act
        var result = Result<string>.Failure("");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("");
    }
}
