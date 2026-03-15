using nem.Mimir.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace nem.Mimir.Domain.Tests.ValueObjects;

public class SystemPromptIdTests
{
    [Fact]
    public void Create_With_Guid_Value_Should_Create_SystemPromptId()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var id = new SystemPromptId(guidValue);

        // Assert
        id.Value.ShouldBe(guidValue);
    }

    [Fact]
    public void New_Should_Generate_New_SystemPromptId()
    {
        // Act
        var id = SystemPromptId.New();

        // Assert
        id.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void New_Should_Generate_Unique_SystemPromptIds()
    {
        // Act
        var id1 = SystemPromptId.New();
        var id2 = SystemPromptId.New();

        // Assert
        id1.Value.ShouldNotBe(id2.Value);
    }

    [Fact]
    public void From_Should_Create_SystemPromptId_From_Guid()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var id = SystemPromptId.From(guidValue);

        // Assert
        id.Value.ShouldBe(guidValue);
    }

    [Fact]
    public void SystemPromptIds_With_Same_Value_Should_Be_Equal()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var id1 = new SystemPromptId(guidValue);
        var id2 = new SystemPromptId(guidValue);

        // Act & Assert
        id1.ShouldBe(id2);
    }

    [Fact]
    public void SystemPromptIds_With_Different_Values_Should_Not_Be_Equal()
    {
        // Arrange
        var id1 = SystemPromptId.New();
        var id2 = SystemPromptId.New();

        // Act & Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void SystemPromptIds_Should_Have_Same_Hash_Code_If_Equal()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var id1 = new SystemPromptId(guidValue);
        var id2 = new SystemPromptId(guidValue);

        // Act & Assert
        id1.GetHashCode().ShouldBe(id2.GetHashCode());
    }

    [Fact]
    public void Equality_Operator_Should_Work()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var id1 = new SystemPromptId(guidValue);
        var id2 = new SystemPromptId(guidValue);
        var id3 = SystemPromptId.New();

        // Act & Assert
        (id1 == id2).ShouldBeTrue();
        (id1 != id3).ShouldBeTrue();
    }

    [Fact]
    public void ToString_Should_Return_Guid_String()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var id = new SystemPromptId(guidValue);

        // Act
        var result = id.ToString();

        // Assert
        result.ShouldBe(guidValue.ToString());
    }
}
