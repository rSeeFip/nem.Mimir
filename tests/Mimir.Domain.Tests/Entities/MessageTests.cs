using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using Shouldly;
using Xunit;

namespace Mimir.Domain.Tests.Entities;

public class MessageTests
{
    [Fact]
    public void Message_Created_Through_Conversation_Should_Have_Valid_Properties()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test Conversation");
        var role = MessageRole.User;
        var content = "Hello!";

        // Act
        var message = conversation.AddMessage(role, content);

        // Assert
        message.ConversationId.ShouldBe(conversation.Id);
        message.Role.ShouldBe(role);
        message.Content.ShouldBe(content);
        message.Id.ShouldNotBe(Guid.Empty);
        message.CreatedAt.ShouldNotBe(DateTimeOffset.MinValue);
        message.Model.ShouldBeNull();
        message.TokenCount.ShouldBeNull();
    }

    [Fact]
    public void Message_With_Model_Should_Store_Model()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var role = MessageRole.Assistant;
        var content = "Response";
        var model = "gpt-4";

        // Act
        var message = conversation.AddMessage(role, content, model);

        // Assert
        message.Model.ShouldBe(model);
    }

    [Fact]
    public void Message_With_Null_Model_Should_Be_Null()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        // Act
        var message = conversation.AddMessage(MessageRole.User, "Content", null);

        // Assert
        message.Model.ShouldBeNull();
    }

    [Fact]
    public void Message_Should_Support_User_Role()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        // Act
        var message = conversation.AddMessage(MessageRole.User, "User message");

        // Assert
        message.Role.ShouldBe(MessageRole.User);
    }

    [Fact]
    public void Message_Should_Support_Assistant_Role()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        // Act
        var message = conversation.AddMessage(MessageRole.Assistant, "Assistant response");

        // Assert
        message.Role.ShouldBe(MessageRole.Assistant);
    }

    [Fact]
    public void Message_Should_Support_System_Role()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        // Act
        var message = conversation.AddMessage(MessageRole.System, "System message");

        // Assert
        message.Role.ShouldBe(MessageRole.System);
    }

    [Fact]
    public void SetTokenCount_With_Valid_Count_Should_Update()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "Hello!");
        var tokenCount = 42;

        // Act
        message.SetTokenCount(tokenCount);

        // Assert
        message.TokenCount.ShouldBe(tokenCount);
    }

    [Fact]
    public void SetTokenCount_With_Zero_Should_Update()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "Hello!");

        // Act
        message.SetTokenCount(0);

        // Assert
        message.TokenCount.ShouldBe(0);
    }

    [Fact]
    public void SetTokenCount_With_Negative_Count_Should_Throw()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "Hello!");

        // Act & Assert
        Should.Throw<ArgumentException>(() => message.SetTokenCount(-1));
    }

    [Fact]
    public void SetTokenCount_Multiple_Times_Should_Update_Last_Value()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var message = conversation.AddMessage(MessageRole.User, "Hello!");

        // Act
        message.SetTokenCount(10);
        message.SetTokenCount(20);

        // Assert
        message.TokenCount.ShouldBe(20);
    }

    [Fact]
    public void Message_Content_Should_Be_Immutable_After_Creation()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var content = "Original content";

        // Act
        var message = conversation.AddMessage(MessageRole.User, content);
        var contentAfter = message.Content;

        // Assert
        contentAfter.ShouldBe(content);
        // Content is private set, cannot be modified after creation
    }

    [Fact]
    public void Message_Role_Should_Be_Immutable_After_Creation()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var role = MessageRole.User;

        // Act
        var message = conversation.AddMessage(role, "Content");
        var roleAfter = message.Role;

        // Assert
        roleAfter.ShouldBe(role);
        // Role is private set, cannot be modified after creation
    }

    [Fact]
    public void Messages_With_Same_Id_Should_Be_Equal()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var msg1 = conversation.AddMessage(MessageRole.User, "Message 1");
        var msg2 = conversation.AddMessage(MessageRole.Assistant, "Message 2");
        
        // Use reflection to set msg2's Id to match msg1's
        msg2.GetType()
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.SetValue(msg2, msg1.Id);

        // Act & Assert
        msg1.ShouldBe(msg2);
    }

    [Fact]
    public void Messages_With_Different_Ids_Should_Not_Be_Equal()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var msg1 = conversation.AddMessage(MessageRole.User, "Message 1");
        var msg2 = conversation.AddMessage(MessageRole.User, "Message 2");

        // Act & Assert
        msg1.ShouldNotBe(msg2);
    }

    [Fact]
    public void Messages_Should_Have_Unique_Ids()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        // Act
        var msg1 = conversation.AddMessage(MessageRole.User, "Message 1");
        var msg2 = conversation.AddMessage(MessageRole.User, "Message 2");

        // Assert
        msg1.Id.ShouldNotBe(msg2.Id);
    }

    [Fact]
    public void Message_CreatedAt_Should_Be_Recent()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        var beforeCreation = DateTimeOffset.UtcNow;

        // Act
        var message = conversation.AddMessage(MessageRole.User, "Hello!");
        var afterCreation = DateTimeOffset.UtcNow;

        // Assert
        message.CreatedAt.ShouldBeGreaterThanOrEqualTo(beforeCreation);
        message.CreatedAt.ShouldBeLessThanOrEqualTo(afterCreation);
    }

    [Fact]
    public void Message_Should_Be_Added_To_Conversation()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        // Act
        var message = conversation.AddMessage(MessageRole.User, "Test message");

        // Assert
        conversation.Messages.ShouldContain(message);
    }

    [Fact]
    public void Conversation_Should_Track_Multiple_Messages()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");

        // Act
        var msg1 = conversation.AddMessage(MessageRole.User, "Message 1");
        var msg2 = conversation.AddMessage(MessageRole.Assistant, "Message 2");
        var msg3 = conversation.AddMessage(MessageRole.User, "Message 3");

        // Assert
        conversation.Messages.Count.ShouldBe(3);
        conversation.Messages.ShouldContain(msg1);
        conversation.Messages.ShouldContain(msg2);
        conversation.Messages.ShouldContain(msg3);
    }
}
