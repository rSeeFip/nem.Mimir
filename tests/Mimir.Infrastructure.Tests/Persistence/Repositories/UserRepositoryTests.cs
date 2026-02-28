namespace Mimir.Infrastructure.Tests.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using Mimir.Infrastructure.Persistence;
using Mimir.Infrastructure.Persistence.Repositories;
using Shouldly;

public class UserRepositoryTests : RepositoryTestBase
{
    private UserRepository CreateRepository() => new(Context);

    private UserRepository CreateRepository(MimirDbContext context) => new(context);

    [Fact]
    public async Task CreateAsync_adds_user_to_context()
    {
        // Arrange
        var repo = CreateRepository();
        var user = User.Create("testuser", "test@example.com", UserRole.User);

        // Act
        var result = await repo.CreateAsync(user);
        await Context.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var retrieved = await readContext.Users.FindAsync(user.Id);
        retrieved.ShouldNotBeNull();
        retrieved.Username.ShouldBe("testuser");
        retrieved.Email.ShouldBe("test@example.com");
        result.Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task GetByIdAsync_returns_user_when_exists()
    {
        // Arrange
        var user = User.Create("getbyid", "getbyid@example.com", UserRole.User);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByIdAsync(user.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Username.ShouldBe("getbyid");
        result.Email.ShouldBe("getbyid@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_not_found()
    {
        // Arrange
        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByExternalIdAsync_returns_user_by_email()
    {
        // Arrange
        var user = User.Create("extuser", "ext@example.com", UserRole.User);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByExternalIdAsync("ext@example.com");

        // Assert
        result.ShouldNotBeNull();
        result.Username.ShouldBe("extuser");
    }

    [Fact]
    public async Task GetByExternalIdAsync_returns_null_when_not_found()
    {
        // Arrange
        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByExternalIdAsync("nonexistent@example.com");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_modifies_existing_user()
    {
        // Arrange
        var user = User.Create("updateuser", "update@example.com", UserRole.User);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        // Act
        await using var updateContext = CreateContext();
        var toUpdate = await updateContext.Users.FindAsync(user.Id);
        toUpdate.ShouldNotBeNull();
        toUpdate.ChangeRole(UserRole.Admin);

        var repo = CreateRepository(updateContext);
        await repo.UpdateAsync(toUpdate);
        await updateContext.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var retrieved = await readContext.Users.FindAsync(user.Id);
        retrieved.ShouldNotBeNull();
        retrieved.Role.ShouldBe(UserRole.Admin);
    }
}
