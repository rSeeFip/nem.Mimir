using Mimir.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Mimir.Domain.Tests.ValueObjects;

public class UserIdTests
{
    [Fact]
    public void Create_With_Guid_Value_Should_Create_UserId()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var userId = new UserId(guidValue);

        // Assert
        userId.Value.ShouldBe(guidValue);
    }

    [Fact]
    public void New_Should_Generate_New_UserId()
    {
        // Act
        var userId = UserId.New();

        // Assert
        userId.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void New_Should_Generate_Unique_UserIds()
    {
        // Act
        var userId1 = UserId.New();
        var userId2 = UserId.New();

        // Assert
        userId1.Value.ShouldNotBe(userId2.Value);
    }

    [Fact]
    public void From_Should_Create_UserId_From_Guid()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var userId = UserId.From(guidValue);

        // Assert
        userId.Value.ShouldBe(guidValue);
    }

    [Fact]
    public void UserIds_With_Same_Value_Should_Be_Equal()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var userId1 = new UserId(guidValue);
        var userId2 = new UserId(guidValue);

        // Act & Assert
        userId1.ShouldBe(userId2);
    }

    [Fact]
    public void UserIds_With_Different_Values_Should_Not_Be_Equal()
    {
        // Arrange
        var userId1 = UserId.New();
        var userId2 = UserId.New();

        // Act & Assert
        userId1.ShouldNotBe(userId2);
    }

    [Fact]
    public void UserIds_Should_Have_Same_Hash_Code_If_Equal()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var userId1 = new UserId(guidValue);
        var userId2 = new UserId(guidValue);

        // Act & Assert
        userId1.GetHashCode().ShouldBe(userId2.GetHashCode());
    }

    [Fact]
    public void Equality_Operator_Should_Work()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var userId1 = new UserId(guidValue);
        var userId2 = new UserId(guidValue);
        var userId3 = UserId.New();

        // Act & Assert
        (userId1 == userId2).ShouldBeTrue();
        (userId1 != userId3).ShouldBeTrue();
    }

    [Fact]
    public void ToString_Should_Return_Guid_String()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var userId = new UserId(guidValue);

        // Act
        var result = userId.ToString();

        // Assert
        result.ShouldBe(guidValue.ToString());
    }

    [Fact]
    public void UserIds_Are_Value_Objects()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var userId1 = new UserId(guidValue);
        var userId2 = new UserId(guidValue);

        // Assert
        // Value objects should be equal even if they're different instances
        userId1.Equals(userId2).ShouldBeTrue();
    }
}
