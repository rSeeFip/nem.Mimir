namespace Mimir.Infrastructure.Tests.Persistence.Repositories;

using Mimir.Domain.Entities;
using Mimir.Infrastructure.Persistence;
using Mimir.Infrastructure.Persistence.Repositories;
using Shouldly;

public class AuditRepositoryTests : RepositoryTestBase
{
    private AuditRepository CreateRepository() => new(Context);

    private AuditRepository CreateRepository(MimirDbContext context) => new(context);

    [Fact]
    public async Task CreateAsync_adds_audit_entry_to_context()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditEntry = AuditEntry.Create(userId, "Login", "User", userId.ToString(), "User logged in", "127.0.0.1");
        var repo = CreateRepository();

        // Act
        var result = await repo.CreateAsync(auditEntry);
        await Context.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var retrieved = await readContext.AuditEntries.FindAsync(auditEntry.Id);
        retrieved.ShouldNotBeNull();
        retrieved.UserId.ShouldBe(userId);
        retrieved.Action.ShouldBe("Login");
        retrieved.EntityType.ShouldBe("User");
        retrieved.EntityId.ShouldBe(userId.ToString());
        retrieved.Details.ShouldBe("User logged in");
        retrieved.IpAddress.ShouldBe("127.0.0.1");
        result.Id.ShouldBe(auditEntry.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_returns_paginated_entries_ordered_by_timestamp_desc()
    {
        // Arrange
        var userId = Guid.NewGuid();

        for (var i = 1; i <= 5; i++)
        {
            var entry = AuditEntry.Create(userId, $"Action{i}", "User", userId.ToString());
            Context.AuditEntries.Add(entry);
        }

        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByUserIdAsync(userId, 1, 3);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(2);
        result.PageNumber.ShouldBe(1);
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public async Task GetByUserIdAsync_returns_second_page()
    {
        // Arrange
        var userId = Guid.NewGuid();

        for (var i = 1; i <= 5; i++)
        {
            var entry = AuditEntry.Create(userId, $"Page2Action{i}", "User");
            Context.AuditEntries.Add(entry);
        }

        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByUserIdAsync(userId, 2, 3);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(5);
        result.PageNumber.ShouldBe(2);
        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task GetByUserIdAsync_returns_empty_for_unknown_user()
    {
        // Arrange
        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByUserIdAsync(Guid.NewGuid(), 1, 10);

        // Assert
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetByEntityAsync_returns_filtered_entries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var targetEntityId = Guid.NewGuid().ToString();

        Context.AuditEntries.Add(AuditEntry.Create(userId, "Create", "Conversation", targetEntityId));
        Context.AuditEntries.Add(AuditEntry.Create(userId, "Update", "Conversation", targetEntityId));
        Context.AuditEntries.Add(AuditEntry.Create(userId, "Delete", "Conversation", Guid.NewGuid().ToString()));
        Context.AuditEntries.Add(AuditEntry.Create(userId, "Create", "User", targetEntityId));

        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByEntityAsync("Conversation", targetEntityId, 1, 10);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.Items.ShouldAllBe(e => e.EntityType == "Conversation" && e.EntityId == targetEntityId);
    }

    [Fact]
    public async Task GetByEntityAsync_returns_empty_when_no_matches()
    {
        // Arrange
        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByEntityAsync("NonExistent", Guid.NewGuid().ToString(), 1, 10);

        // Assert
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetByEntityAsync_returns_paginated_results()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid().ToString();

        for (var i = 1; i <= 5; i++)
        {
            Context.AuditEntries.Add(AuditEntry.Create(userId, $"Action{i}", "Conversation", entityId));
        }

        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByEntityAsync("Conversation", entityId, 1, 3);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(2);
    }
}
