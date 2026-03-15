using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Events;
using Shouldly;
using Xunit;

namespace nem.Mimir.Domain.Tests.Events;

public class DomainEventTests
{
    [Fact]
    public void UserCreatedEvent_Should_Store_UserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var username = "john_doe";

        // Act
        var @event = new UserCreatedEvent(userId, username);

        // Assert
        @event.UserId.ShouldBe(userId);
    }

    [Fact]
    public void UserCreatedEvent_Should_Store_Username()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var username = "john_doe";

        // Act
        var @event = new UserCreatedEvent(userId, username);

        // Assert
        @event.Username.ShouldBe(username);
    }

    [Fact]
    public void UserCreatedEvent_Should_Be_IDomainEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var username = "john_doe";

        // Act
        var @event = new UserCreatedEvent(userId, username);

        // Assert
        @event.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void UserCreatedEvent_Should_Support_Equality()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var username = "john_doe";
        var event1 = new UserCreatedEvent(userId, username);
        var event2 = new UserCreatedEvent(userId, username);

        // Act & Assert
        event1.Equals(event2).ShouldBeTrue();
    }

    [Fact]
    public void UserCreatedEvent_With_Different_UserId_Should_Not_Be_Equal()
    {
        // Arrange
        var username = "john_doe";
        var event1 = new UserCreatedEvent(Guid.NewGuid(), username);
        var event2 = new UserCreatedEvent(Guid.NewGuid(), username);

        // Act & Assert
        event1.Equals(event2).ShouldBeFalse();
    }

    [Fact]
    public void UserCreatedEvent_With_Different_Username_Should_Not_Be_Equal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var event1 = new UserCreatedEvent(userId, "john_doe");
        var event2 = new UserCreatedEvent(userId, "jane_doe");

        // Act & Assert
        event1.Equals(event2).ShouldBeFalse();
    }

    [Fact]
    public void ConversationCreatedEvent_Should_Store_ConversationId()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var @event = new ConversationCreatedEvent(conversationId, userId);

        // Assert
        @event.ConversationId.ShouldBe(conversationId);
    }

    [Fact]
    public void ConversationCreatedEvent_Should_Store_UserId()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var @event = new ConversationCreatedEvent(conversationId, userId);

        // Assert
        @event.UserId.ShouldBe(userId);
    }

    [Fact]
    public void ConversationCreatedEvent_Should_Be_IDomainEvent()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var @event = new ConversationCreatedEvent(conversationId, userId);

        // Assert
        @event.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void ConversationCreatedEvent_Should_Support_Equality()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var event1 = new ConversationCreatedEvent(conversationId, userId);
        var event2 = new ConversationCreatedEvent(conversationId, userId);

        // Act & Assert
        event1.Equals(event2).ShouldBeTrue();
    }

    [Fact]
    public void ConversationCreatedEvent_With_Different_ConversationId_Should_Not_Be_Equal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var event1 = new ConversationCreatedEvent(Guid.NewGuid(), userId);
        var event2 = new ConversationCreatedEvent(Guid.NewGuid(), userId);

        // Act & Assert
        event1.Equals(event2).ShouldBeFalse();
    }

    [Fact]
    public void ConversationCreatedEvent_With_Different_UserId_Should_Not_Be_Equal()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var event1 = new ConversationCreatedEvent(conversationId, Guid.NewGuid());
        var event2 = new ConversationCreatedEvent(conversationId, Guid.NewGuid());

        // Act & Assert
        event1.Equals(event2).ShouldBeFalse();
    }

    [Fact]
    public void MessageSentEvent_Should_Store_MessageId()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var role = MessageRole.User;

        // Act
        var @event = new MessageSentEvent(messageId, conversationId, role);

        // Assert
        @event.MessageId.ShouldBe(messageId);
    }

    [Fact]
    public void MessageSentEvent_Should_Store_ConversationId()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var role = MessageRole.User;

        // Act
        var @event = new MessageSentEvent(messageId, conversationId, role);

        // Assert
        @event.ConversationId.ShouldBe(conversationId);
    }

    [Fact]
    public void MessageSentEvent_Should_Store_Role()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var role = MessageRole.Assistant;

        // Act
        var @event = new MessageSentEvent(messageId, conversationId, role);

        // Assert
        @event.Role.ShouldBe(role);
    }

    [Fact]
    public void MessageSentEvent_Should_Be_IDomainEvent()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var role = MessageRole.User;

        // Act
        var @event = new MessageSentEvent(messageId, conversationId, role);

        // Assert
        @event.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void MessageSentEvent_Should_Support_Equality()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var role = MessageRole.User;
        var event1 = new MessageSentEvent(messageId, conversationId, role);
        var event2 = new MessageSentEvent(messageId, conversationId, role);

        // Act & Assert
        event1.Equals(event2).ShouldBeTrue();
    }

    [Fact]
    public void MessageSentEvent_With_Different_MessageId_Should_Not_Be_Equal()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var role = MessageRole.User;
        var event1 = new MessageSentEvent(Guid.NewGuid(), conversationId, role);
        var event2 = new MessageSentEvent(Guid.NewGuid(), conversationId, role);

        // Act & Assert
        event1.Equals(event2).ShouldBeFalse();
    }

    [Fact]
    public void MessageSentEvent_With_Different_ConversationId_Should_Not_Be_Equal()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var role = MessageRole.User;
        var event1 = new MessageSentEvent(messageId, Guid.NewGuid(), role);
        var event2 = new MessageSentEvent(messageId, Guid.NewGuid(), role);

        // Act & Assert
        event1.Equals(event2).ShouldBeFalse();
    }

    [Fact]
    public void MessageSentEvent_With_Different_Role_Should_Not_Be_Equal()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var event1 = new MessageSentEvent(messageId, conversationId, MessageRole.User);
        var event2 = new MessageSentEvent(messageId, conversationId, MessageRole.Assistant);

        // Act & Assert
        event1.Equals(event2).ShouldBeFalse();
    }

    [Fact]
    public void MessageSentEvent_Should_Support_All_Roles()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        // Act
        var userEvent = new MessageSentEvent(messageId, conversationId, MessageRole.User);
        var assistantEvent = new MessageSentEvent(messageId, conversationId, MessageRole.Assistant);
        var systemEvent = new MessageSentEvent(messageId, conversationId, MessageRole.System);

        // Assert
        userEvent.Role.ShouldBe(MessageRole.User);
        assistantEvent.Role.ShouldBe(MessageRole.Assistant);
        systemEvent.Role.ShouldBe(MessageRole.System);
    }
}
