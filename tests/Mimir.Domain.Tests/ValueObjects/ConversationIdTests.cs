using Mimir.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Mimir.Domain.Tests.ValueObjects;

public class ConversationIdTests
{
    [Fact]
    public void Create_With_Guid_Value_Should_Create_ConversationId()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var conversationId = new ConversationId(guidValue);

        // Assert
        conversationId.Value.ShouldBe(guidValue);
    }

    [Fact]
    public void New_Should_Generate_New_ConversationId()
    {
        // Act
        var conversationId = ConversationId.New();

        // Assert
        conversationId.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void New_Should_Generate_Unique_ConversationIds()
    {
        // Act
        var conversationId1 = ConversationId.New();
        var conversationId2 = ConversationId.New();

        // Assert
        conversationId1.Value.ShouldNotBe(conversationId2.Value);
    }

    [Fact]
    public void From_Should_Create_ConversationId_From_Guid()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var conversationId = ConversationId.From(guidValue);

        // Assert
        conversationId.Value.ShouldBe(guidValue);
    }

    [Fact]
    public void ConversationIds_With_Same_Value_Should_Be_Equal()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var conversationId1 = new ConversationId(guidValue);
        var conversationId2 = new ConversationId(guidValue);

        // Act & Assert
        conversationId1.ShouldBe(conversationId2);
    }

    [Fact]
    public void ConversationIds_With_Different_Values_Should_Not_Be_Equal()
    {
        // Arrange
        var conversationId1 = ConversationId.New();
        var conversationId2 = ConversationId.New();

        // Act & Assert
        conversationId1.ShouldNotBe(conversationId2);
    }

    [Fact]
    public void ConversationIds_Should_Have_Same_Hash_Code_If_Equal()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var conversationId1 = new ConversationId(guidValue);
        var conversationId2 = new ConversationId(guidValue);

        // Act & Assert
        conversationId1.GetHashCode().ShouldBe(conversationId2.GetHashCode());
    }

    [Fact]
    public void Equality_Operator_Should_Work()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var conversationId1 = new ConversationId(guidValue);
        var conversationId2 = new ConversationId(guidValue);
        var conversationId3 = ConversationId.New();

        // Act & Assert
        (conversationId1 == conversationId2).ShouldBeTrue();
        (conversationId1 != conversationId3).ShouldBeTrue();
    }

    [Fact]
    public void ToString_Should_Return_Guid_String()
    {
        // Arrange
        var guidValue = Guid.NewGuid();
        var conversationId = new ConversationId(guidValue);

        // Act
        var result = conversationId.ToString();

        // Assert
        result.ShouldBe(guidValue.ToString());
    }

    [Fact]
    public void ConversationIds_Are_Value_Objects()
    {
        // Arrange
        var guidValue = Guid.NewGuid();

        // Act
        var conversationId1 = new ConversationId(guidValue);
        var conversationId2 = new ConversationId(guidValue);

        // Assert
        // Value objects should be equal even if they're different instances
        conversationId1.Equals(conversationId2).ShouldBeTrue();
    }
}
