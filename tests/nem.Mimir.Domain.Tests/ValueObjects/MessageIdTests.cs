using nem.Mimir.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace nem.Mimir.Domain.Tests.ValueObjects;

public class MessageIdTests
{
    [Fact]
    public void Create_With_Guid_Value_Should_Create_MessageId()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var messageId = new MessageId(guidValue);

        // Assert
        messageId.Value.ShouldBe(guidValue);
    }

    [Fact]
    public void New_Should_Generate_New_MessageId()
    {
        // Act
        var messageId = MessageId.New();

        // Assert
        messageId.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void New_Should_Generate_Unique_MessageIds()
    {
        // Act
        var messageId1 = MessageId.New();
        var messageId2 = MessageId.New();

        // Assert
        messageId1.Value.ShouldNotBe(messageId2.Value);
    }

    [Fact]
    public void From_Should_Create_MessageId_From_Guid()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var messageId = MessageId.From(guidValue);

        // Assert
        messageId.Value.ShouldBe(guidValue);
    }

    [Fact]
    public void MessageIds_With_Same_Value_Should_Be_Equal()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var messageId1 = new MessageId(guidValue);
        var messageId2 = new MessageId(guidValue);

        // Act & Assert
        messageId1.ShouldBe(messageId2);
    }

    [Fact]
    public void MessageIds_With_Different_Values_Should_Not_Be_Equal()
    {
        // Arrange
        var messageId1 = MessageId.New();
        var messageId2 = MessageId.New();

        // Act & Assert
        messageId1.ShouldNotBe(messageId2);
    }

    [Fact]
    public void MessageIds_Should_Have_Same_Hash_Code_If_Equal()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var messageId1 = new MessageId(guidValue);
        var messageId2 = new MessageId(guidValue);

        // Act & Assert
        messageId1.GetHashCode().ShouldBe(messageId2.GetHashCode());
    }

    [Fact]
    public void Equality_Operator_Should_Work()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var messageId1 = new MessageId(guidValue);
        var messageId2 = new MessageId(guidValue);
        var messageId3 = MessageId.New();

        // Act & Assert
        (messageId1 == messageId2).ShouldBeTrue();
        (messageId1 != messageId3).ShouldBeTrue();
    }

    [Fact]
    public void ToString_Should_Return_Guid_String()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var messageId = new MessageId(guidValue);

        // Act
        var result = messageId.ToString();

        // Assert
        result.ShouldBe(guidValue.ToString());
    }

    [Fact]
    public void MessageIds_Are_Value_Objects()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var messageId1 = new MessageId(guidValue);
        var messageId2 = new MessageId(guidValue);

        // Assert
        // Value objects should be equal even if they're different instances
        messageId1.Equals(messageId2).ShouldBeTrue();
    }
}
