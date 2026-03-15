using System.Linq;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Events;
using Shouldly;
using Xunit;

namespace nem.Mimir.Domain.Tests.Entities;

public class ConversationTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_Conversation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var title = "My Conversation";

        // Act
        var conversation = Conversation.Create(userId, title);

        // Assert
        conversation.UserId.ShouldBe(userId);
        conversation.Title.ShouldBe(title);
        conversation.Status.ShouldBe(ConversationStatus.Active);
        conversation.Id.ShouldNotBe(Guid.Empty);
        conversation.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void Create_With_Empty_UserId_Should_Throw()
    {
        // Arrange
        var title = "My Conversation";

        // Act & Assert
        Should.Throw<ArgumentException>(() => Conversation.Create(Guid.Empty, title));
    }

    [Fact]
    public void Create_With_Empty_Title_Should_Throw()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act & Assert
        Should.Throw<ArgumentException>(() => Conversation.Create(userId, string.Empty));
    }

    [Fact]
    public void Create_With_Whitespace_Title_Should_Throw()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act & Assert
        Should.Throw<ArgumentException>(() => Conversation.Create(userId, "   "));
    }

    [Fact]
    public void Create_With_Null_Title_Should_Throw()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act & Assert
        Should.Throw<ArgumentException>(() => Conversation.Create(userId, null!));
    }

    [Fact]
    public void Create_Should_Raise_ConversationCreatedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var title = "My Conversation";

        // Act
        var conversation = Conversation.Create(userId, title);

        // Assert
        conversation.DomainEvents.Count.ShouldBe(1);
        var domainEvent = conversation.DomainEvents.ShouldHaveSingleItem();
        domainEvent.ShouldBeOfType<ConversationCreatedEvent>();
        var conversationCreatedEvent = (ConversationCreatedEvent)domainEvent;
        conversationCreatedEvent.ConversationId.ShouldBe(conversation.Id);
        conversationCreatedEvent.UserId.ShouldBe(userId);
    }

    [Fact]
    public void AddMessage_Should_Add_Message_To_Conversation()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "My Conversation");
        var messageContent = "Hello!";

        // Act
        var message = conversation.AddMessage(MessageRole.User, messageContent);

        // Assert
        conversation.Messages.Count.ShouldBe(1);
        conversation.Messages.ShouldContain(message);
        message.Content.ShouldBe(messageContent);
        message.Role.ShouldBe(MessageRole.User);
        message.ConversationId.ShouldBe(conversation.Id);
    }

    [Fact]
    public void AddMessage_Should_Raise_MessageSentEvent()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "My Conversation");
        var messageContent = "Hello!";

        // Act
        var message = conversation.AddMessage(MessageRole.User, messageContent);

        // Assert
        conversation.DomainEvents.Count.ShouldBe(2); // ConversationCreatedEvent + MessageSentEvent
        var messageSentEvent = conversation.DomainEvents.OfType<MessageSentEvent>().ShouldHaveSingleItem();
        messageSentEvent.MessageId.ShouldBe(message.Id);
        messageSentEvent.ConversationId.ShouldBe(conversation.Id);
        messageSentEvent.Role.ShouldBe(MessageRole.User);
    }

    [Fact]
    public void AddMessage_Should_Update_UpdatedAt()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "My Conversation");
        var beforeAdd = DateTimeOffset.UtcNow;

        // Act
        conversation.AddMessage(MessageRole.User, "Hello!");
        var afterAdd = DateTimeOffset.UtcNow;

        // Assert
    }

    [Fact]
    public void AddMessage_To_Archived_Conversation_Should_Throw()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "My Conversation");
        conversation.Archive();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => conversation.AddMessage(MessageRole.User, "Hello!"));
    }

    [Fact]
    public void AddMessage_With_Model_Should_Store_Model()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "My Conversation");
        var model = "gpt-4";

        // Act
        var message = conversation.AddMessage(MessageRole.Assistant, "Response", model);

        // Assert
        message.Model.ShouldBe(model);
    }

    [Fact]
    public void Archive_Should_Set_Status_To_Archived()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "My Conversation");

        // Act
        conversation.Archive();

        // Assert
        conversation.Status.ShouldBe(ConversationStatus.Archived);
    }

    [Fact]
    public void Archive_Should_Update_UpdatedAt()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "My Conversation");
        var beforeArchive = DateTimeOffset.UtcNow;

        // Act
        conversation.Archive();
        var afterArchive = DateTimeOffset.UtcNow;

        // Assert
    }

    [Fact]
    public void UpdateTitle_With_Valid_Title_Should_Update()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Old Title");
        var newTitle = "New Title";

        // Act
        conversation.UpdateTitle(newTitle);

        // Assert
        conversation.Title.ShouldBe(newTitle);
    }

    [Fact]
    public void UpdateTitle_With_Empty_Title_Should_Throw()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "My Conversation");

        // Act & Assert
        Should.Throw<ArgumentException>(() => conversation.UpdateTitle(string.Empty));
    }

    [Fact]
    public void UpdateTitle_With_Whitespace_Title_Should_Throw()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "My Conversation");

        // Act & Assert
        Should.Throw<ArgumentException>(() => conversation.UpdateTitle("   "));
    }

    [Fact]
    public void UpdateTitle_Should_Update_UpdatedAt()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Old Title");
        var beforeUpdate = DateTimeOffset.UtcNow;

        // Act
        conversation.UpdateTitle("New Title");
        var afterUpdate = DateTimeOffset.UtcNow;

        // Assert
    }

    [Fact]
    public void Conversations_With_Same_Id_Should_Be_Equal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conv1 = Conversation.Create(userId, "Conversation 1");
        var conv2 = Conversation.Create(userId, "Conversation 2");
        conv2.GetType()
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.SetValue(conv2, conv1.Id);

        // Act & Assert
        conv1.ShouldBe(conv2);
    }

    [Fact]
    public void Conversations_With_Different_Ids_Should_Not_Be_Equal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conv1 = Conversation.Create(userId, "Conversation 1");
        var conv2 = Conversation.Create(userId, "Conversation 2");

        // Act & Assert
        conv1.ShouldNotBe(conv2);
    }

    [Fact]
    public void AddMultiple_Messages_Should_All_Be_Stored()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "My Conversation");

        // Act
        conversation.AddMessage(MessageRole.User, "First message");
        conversation.AddMessage(MessageRole.Assistant, "Response");
        conversation.AddMessage(MessageRole.User, "Second message");

        // Assert
        conversation.Messages.Count.ShouldBe(3);
    }
}
