namespace nem.Mimir.Infrastructure.Tests.Persistence.Repositories;

using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Infrastructure.Persistence;
using Shouldly;

public class UnitOfWorkTests : RepositoryTestBase
{
    private Infrastructure.Persistence.UnitOfWork CreateUnitOfWork() => new(Context);

    private Infrastructure.Persistence.UnitOfWork CreateUnitOfWork(MimirDbContext context) => new(context);

    [Fact]
    public async Task SaveChangesAsync_persists_added_entities()
    {
        // Arrange
        var user = User.Create("uowuser", "uow@example.com", UserRole.User);
        Context.Users.Add(user);

        var uow = CreateUnitOfWork();

        // Act
        var result = await uow.SaveChangesAsync();

        // Assert
        result.ShouldBeGreaterThan(0);

        await using var readContext = CreateContext();
        var retrieved = await readContext.Users.FindAsync(user.Id);
        retrieved.ShouldNotBeNull();
        retrieved.Username.ShouldBe("uowuser");
    }

    [Fact]
    public async Task SaveChangesAsync_persists_modified_entities()
    {
        // Arrange
        var user = User.Create("uowmod", "uowmod@example.com", UserRole.User);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        // Act
        await using var updateContext = CreateContext();
        var toUpdate = await updateContext.Users.FindAsync(user.Id);
        toUpdate.ShouldNotBeNull();
        toUpdate.ChangeRole(UserRole.Admin);

        var uow = CreateUnitOfWork(updateContext);
        var result = await uow.SaveChangesAsync();

        // Assert
        result.ShouldBeGreaterThan(0);

        await using var readContext = CreateContext();
        var retrieved = await readContext.Users.FindAsync(user.Id);
        retrieved.ShouldNotBeNull();
        retrieved.Role.ShouldBe(UserRole.Admin);
    }

    [Fact]
    public async Task SaveChangesAsync_returns_zero_when_no_changes()
    {
        // Arrange
        var uow = CreateUnitOfWork();

        // Act
        var result = await uow.SaveChangesAsync();

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public async Task SaveChangesAsync_supports_cancellation_token()
    {
        // Arrange
        var user = User.Create("canceluser", "cancel@example.com", UserRole.User);
        Context.Users.Add(user);

        var uow = CreateUnitOfWork();
        using var cts = new CancellationTokenSource();

        // Act
        var result = await uow.SaveChangesAsync(cts.Token);

        // Assert
        result.ShouldBeGreaterThan(0);
    }
}
