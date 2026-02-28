using Mimir.Domain.Entities;
using Shouldly;
using Xunit;

namespace Mimir.Domain.Tests.Entities;

public class AuditEntryTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_AuditEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var action = "Login";
        var entityType = "User";

        // Act
        var auditEntry = AuditEntry.Create(userId, action, entityType);

        // Assert
        auditEntry.UserId.ShouldBe(userId);
        auditEntry.Action.ShouldBe(action);
        auditEntry.EntityType.ShouldBe(entityType);
        auditEntry.Id.ShouldNotBe(Guid.Empty);
        auditEntry.Timestamp.ShouldNotBe(DateTimeOffset.MinValue);
        auditEntry.EntityId.ShouldBeNull();
        auditEntry.Details.ShouldBeNull();
        auditEntry.IpAddress.ShouldBeNull();
    }

    [Fact]
    public void Create_With_Optional_Fields_Should_Store_All_Values()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var action = "Create";
        var entityType = "Conversation";
        var entityId = Guid.NewGuid().ToString();
        var details = "Created new conversation with title 'Test'";
        var ipAddress = "192.168.1.1";

        // Act
        var auditEntry = AuditEntry.Create(userId, action, entityType, entityId, details, ipAddress);

        // Assert
        auditEntry.UserId.ShouldBe(userId);
        auditEntry.Action.ShouldBe(action);
        auditEntry.EntityType.ShouldBe(entityType);
        auditEntry.EntityId.ShouldBe(entityId);
        auditEntry.Details.ShouldBe(details);
        auditEntry.IpAddress.ShouldBe(ipAddress);
    }

    [Fact]
    public void Create_With_Empty_UserId_Should_Throw()
    {
        // Arrange
        var action = "Login";
        var entityType = "User";

        // Act & Assert
        Should.Throw<ArgumentException>(() => AuditEntry.Create(Guid.Empty, action, entityType));
    }

    [Fact]
    public void Create_With_Empty_Action_Should_Throw()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityType = "User";

        // Act & Assert
        Should.Throw<ArgumentException>(() => AuditEntry.Create(userId, string.Empty, entityType));
    }

    [Fact]
    public void Create_With_Whitespace_Action_Should_Throw()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityType = "User";

        // Act & Assert
        Should.Throw<ArgumentException>(() => AuditEntry.Create(userId, "   ", entityType));
    }

    [Fact]
    public void Create_With_Null_Action_Should_Throw()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityType = "User";

        // Act & Assert
        Should.Throw<ArgumentException>(() => AuditEntry.Create(userId, null!, entityType));
    }

    [Fact]
    public void Create_With_Empty_EntityType_Should_Throw()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var action = "Login";

        // Act & Assert
        Should.Throw<ArgumentException>(() => AuditEntry.Create(userId, action, string.Empty));
    }

    [Fact]
    public void Create_With_Whitespace_EntityType_Should_Throw()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var action = "Login";

        // Act & Assert
        Should.Throw<ArgumentException>(() => AuditEntry.Create(userId, action, "   "));
    }

    [Fact]
    public void Create_With_Null_EntityType_Should_Throw()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var action = "Login";

        // Act & Assert
        Should.Throw<ArgumentException>(() => AuditEntry.Create(userId, action, null!));
    }

    [Fact]
    public void Create_With_Various_Actions_Should_Work()
    {
        // Arrange & Act
        var createEntry = AuditEntry.Create(Guid.NewGuid(), "Create", "Conversation");
        var updateEntry = AuditEntry.Create(Guid.NewGuid(), "Update", "Message");
        var deleteEntry = AuditEntry.Create(Guid.NewGuid(), "Delete", "User");
        var loginEntry = AuditEntry.Create(Guid.NewGuid(), "Login", "User");

        // Assert
        createEntry.Action.ShouldBe("Create");
        updateEntry.Action.ShouldBe("Update");
        deleteEntry.Action.ShouldBe("Delete");
        loginEntry.Action.ShouldBe("Login");
    }

    [Fact]
    public void Create_With_Various_EntityTypes_Should_Work()
    {
        // Arrange & Act
        var userEntry = AuditEntry.Create(Guid.NewGuid(), "Login", "User");
        var conversationEntry = AuditEntry.Create(Guid.NewGuid(), "Create", "Conversation");
        var messageEntry = AuditEntry.Create(Guid.NewGuid(), "Create", "Message");

        // Assert
        userEntry.EntityType.ShouldBe("User");
        conversationEntry.EntityType.ShouldBe("Conversation");
        messageEntry.EntityType.ShouldBe("Message");
    }

    [Fact]
    public void Timestamp_Should_Be_Current_Time()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var beforeCreation = DateTimeOffset.UtcNow;

        // Act
        var auditEntry = AuditEntry.Create(userId, "Login", "User");
        var afterCreation = DateTimeOffset.UtcNow;

        // Assert
        auditEntry.Timestamp.ShouldBeGreaterThanOrEqualTo(beforeCreation);
        auditEntry.Timestamp.ShouldBeLessThanOrEqualTo(afterCreation);
    }

    [Fact]
    public void Create_With_Null_EntityId_Should_Work()
    {
        // Arrange & Act
        var auditEntry = AuditEntry.Create(Guid.NewGuid(), "Login", "User", null);

        // Assert
        auditEntry.EntityId.ShouldBeNull();
    }

    [Fact]
    public void Create_With_Null_Details_Should_Work()
    {
        // Arrange & Act
        var auditEntry = AuditEntry.Create(Guid.NewGuid(), "Login", "User", details: null);

        // Assert
        auditEntry.Details.ShouldBeNull();
    }

    [Fact]
    public void Create_With_Null_IpAddress_Should_Work()
    {
        // Arrange & Act
        var auditEntry = AuditEntry.Create(Guid.NewGuid(), "Login", "User", ipAddress: null);

        // Assert
        auditEntry.IpAddress.ShouldBeNull();
    }

    [Fact]
    public void AuditEntries_With_Same_Id_Should_Be_Equal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entry1 = AuditEntry.Create(userId, "Login", "User");
        var entry2 = AuditEntry.Create(userId, "Logout", "User");
        entry2.GetType()
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.SetValue(entry2, entry1.Id);

        // Act & Assert
        entry1.ShouldBe(entry2);
    }

    [Fact]
    public void AuditEntries_With_Different_Ids_Should_Not_Be_Equal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entry1 = AuditEntry.Create(userId, "Login", "User");
        var entry2 = AuditEntry.Create(userId, "Login", "User");

        // Act & Assert
        entry1.ShouldNotBe(entry2);
    }

    [Fact]
    public void Create_With_Complex_Details_Should_Store_Json()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var details = "{\"conversationTitle\": \"Test Conversation\", \"messageCount\": 5}";

        // Act
        var auditEntry = AuditEntry.Create(userId, "Create", "Conversation", details: details);

        // Assert
        auditEntry.Details.ShouldBe(details);
    }

    [Fact]
    public void Create_Should_Generate_Unique_Ids()
    {
        // Arrange & Act
        var entry1 = AuditEntry.Create(Guid.NewGuid(), "Login", "User");
        var entry2 = AuditEntry.Create(Guid.NewGuid(), "Login", "User");

        // Assert
        entry1.Id.ShouldNotBe(entry2.Id);
    }
}
