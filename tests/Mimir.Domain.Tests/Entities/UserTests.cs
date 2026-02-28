using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using Mimir.Domain.Events;
using Shouldly;
using Xunit;

namespace Mimir.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_User()
    {
        // Arrange
        var username = "john_doe";
        var email = "john@example.com";
        var role = UserRole.User;

        // Act
        var user = User.Create(username, email, role);

        // Assert
        user.Username.ShouldBe(username);
        user.Email.ShouldBe(email);
        user.Role.ShouldBe(role);
        user.Id.ShouldNotBe(Guid.Empty);
        user.CreatedAt.ShouldNotBe(DateTimeOffset.MinValue);
    }

    [Fact]
    public void Create_With_Empty_Username_Should_Throw()
    {
        // Arrange
        var email = "john@example.com";
        var role = UserRole.User;

        // Act & Assert
        Should.Throw<ArgumentException>(() => User.Create(string.Empty, email, role));
    }

    [Fact]
    public void Create_With_Whitespace_Username_Should_Throw()
    {
        // Arrange
        var email = "john@example.com";
        var role = UserRole.User;

        // Act & Assert
        Should.Throw<ArgumentException>(() => User.Create("   ", email, role));
    }

    [Fact]
    public void Create_With_Null_Username_Should_Throw()
    {
        // Arrange
        var email = "john@example.com";
        var role = UserRole.User;

        // Act & Assert
        Should.Throw<ArgumentException>(() => User.Create(null!, email, role));
    }

    [Fact]
    public void Create_With_Empty_Email_Should_Throw()
    {
        // Arrange
        var username = "john_doe";
        var role = UserRole.User;

        // Act & Assert
        Should.Throw<ArgumentException>(() => User.Create(username, string.Empty, role));
    }

    [Fact]
    public void Create_With_Whitespace_Email_Should_Throw()
    {
        // Arrange
        var username = "john_doe";
        var role = UserRole.User;

        // Act & Assert
        Should.Throw<ArgumentException>(() => User.Create(username, "   ", role));
    }

    [Fact]
    public void Create_With_Null_Email_Should_Throw()
    {
        // Arrange
        var username = "john_doe";
        var role = UserRole.User;

        // Act & Assert
        Should.Throw<ArgumentException>(() => User.Create(username, null!, role));
    }

    [Fact]
    public void Create_Should_Raise_UserCreatedEvent()
    {
        // Arrange
        var username = "john_doe";
        var email = "john@example.com";
        var role = UserRole.User;

        // Act
        var user = User.Create(username, email, role);

        // Assert
        user.DomainEvents.Count.ShouldBe(1);
        var domainEvent = user.DomainEvents.ShouldHaveSingleItem();
        domainEvent.ShouldBeOfType<UserCreatedEvent>();
        var userCreatedEvent = (UserCreatedEvent)domainEvent;
        userCreatedEvent.UserId.ShouldBe(user.Id);
        userCreatedEvent.Username.ShouldBe(username);
    }

    [Fact]
    public void UpdateLastLogin_Should_Set_LastLoginAt()
    {
        // Arrange
        var user = User.Create("john_doe", "john@example.com", UserRole.User);
        var beforeUpdate = DateTimeOffset.UtcNow;

        // Act
        user.UpdateLastLogin();
        var afterUpdate = DateTimeOffset.UtcNow;

        // Assert
        user.LastLoginAt.ShouldNotBeNull();
        user.LastLoginAt!.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
        user.LastLoginAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate);
    }

    [Fact]
    public void ChangeRole_Should_Update_Role_And_UpdatedAt()
    {
        // Arrange
        var user = User.Create("john_doe", "john@example.com", UserRole.User);
        var beforeChange = DateTimeOffset.UtcNow;
        System.Threading.Thread.Sleep(10); // Ensure time difference

        // Act
        user.ChangeRole(UserRole.Admin);
        var afterChange = DateTimeOffset.UtcNow;

        // Assert
        user.Role.ShouldBe(UserRole.Admin);
        user.UpdatedAt.ShouldNotBeNull();
        user.UpdatedAt!.Value.ShouldBeGreaterThanOrEqualTo(beforeChange);
        user.UpdatedAt.Value.ShouldBeLessThanOrEqualTo(afterChange);
    }

    [Fact]
    public void Users_With_Same_Id_Should_Be_Equal()
    {
        // Arrange
        var user1 = User.Create("john_doe", "john@example.com", UserRole.User);
        var user2Id = user1.Id;
        var user2 = User.Create("jane_doe", "jane@example.com", UserRole.Admin);
        user2.GetType()
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.SetValue(user2, user2Id);

        // Act & Assert
        user1.ShouldBe(user2);
    }

    [Fact]
    public void Users_With_Different_Ids_Should_Not_Be_Equal()
    {
        // Arrange
        var user1 = User.Create("john_doe", "john@example.com", UserRole.User);
        var user2 = User.Create("jane_doe", "jane@example.com", UserRole.User);

        // Act & Assert
        user1.ShouldNotBe(user2);
    }

    [Fact]
    public void User_Equality_Operator_Should_Work()
    {
        // Arrange
        var user1 = User.Create("john_doe", "john@example.com", UserRole.User);
        var user2 = User.Create("jane_doe", "jane@example.com", UserRole.Admin);
        user2.GetType()
            .GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.SetValue(user2, user1.Id);

        // Act & Assert
        (user1 == user2).ShouldBeTrue();
    }

    [Fact]
    public void User_Inequality_Operator_Should_Work()
    {
        // Arrange
        var user1 = User.Create("john_doe", "john@example.com", UserRole.User);
        var user2 = User.Create("jane_doe", "jane@example.com", UserRole.User);

        // Act & Assert
        (user1 != user2).ShouldBeTrue();
    }

    [Fact]
    public void Create_With_Admin_Role_Should_Set_Correct_Role()
    {
        // Arrange & Act
        var user = User.Create("admin_user", "admin@example.com", UserRole.Admin);

        // Assert
        user.Role.ShouldBe(UserRole.Admin);
    }

    [Fact]
    public void Create_Should_Have_Empty_LastLoginAt()
    {
        // Arrange & Act
        var user = User.Create("john_doe", "john@example.com", UserRole.User);

        // Assert
        user.LastLoginAt.ShouldBeNull();
    }
}
