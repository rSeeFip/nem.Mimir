namespace nem.Mimir.Infrastructure.Tests.Interceptors;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Infrastructure.Persistence;
using nem.Mimir.Infrastructure.Persistence.Interceptors;
using NSubstitute;
using Shouldly;
using Testcontainers.PostgreSql;

public sealed class AuditableEntitySoftDeleteTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private MimirDbContext _context = null!;
    private ICurrentUserService _currentUserService = null!;
    private IDateTimeService _dateTimeService = null!;

    private static readonly DateTimeOffset FixedTime =
        new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DeleteTime =
        new(2025, 7, 1, 10, 30, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.UserId.Returns("test-user-id");
        _currentUserService.IsAuthenticated.Returns(true);

        _dateTimeService = Substitute.For<IDateTimeService>();
        _dateTimeService.UtcNow.Returns(FixedTime);

        _context = CreateContext();
        await _context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private MimirDbContext CreateContext()
    {
        var interceptor = new AuditableEntityInterceptor(_currentUserService, _dateTimeService);

        var options = new DbContextOptionsBuilder<MimirDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(interceptor)
            .Options;

        return new MimirDbContext(options);
    }

    // ---------------------------------------------------------------
    // Interceptor behavior: Deleted → Modified, IsDeleted=true
    // ---------------------------------------------------------------

    [Fact]
    public async Task Deleting_auditable_entity_sets_IsDeleted_and_DeletedAt()
    {
        // Arrange
        var user = User.Create("softdel", "softdel@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Switch to delete time
        _dateTimeService.UtcNow.Returns(DeleteTime);

        // Act
        await using var deleteContext = CreateContext();
        var toDelete = await deleteContext.Users.FindAsync(user.Id);
        toDelete.ShouldNotBeNull();
        deleteContext.Users.Remove(toDelete);
        await deleteContext.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var raw = await readContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        raw.ShouldNotBeNull();
        raw.IsDeleted.ShouldBeTrue();
        raw.DeletedAt.ShouldBe(DeleteTime);
    }

    [Fact]
    public async Task Deleting_auditable_entity_changes_state_from_Deleted_to_Modified()
    {
        // Arrange
        var user = User.Create("statetest", "state@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _dateTimeService.UtcNow.Returns(DeleteTime);

        // Act — track state before and after save
        await using var deleteContext = CreateContext();
        var toDelete = await deleteContext.Users.FindAsync(user.Id);
        toDelete.ShouldNotBeNull();
        deleteContext.Users.Remove(toDelete);

        // After Remove() but before SaveChanges, state is Deleted
        var entryBeforeSave = deleteContext.Entry(toDelete);
        entryBeforeSave.State.ShouldBe(EntityState.Deleted);

        await deleteContext.SaveChangesAsync();

        // After save, the interceptor converted it to Modified → now Unchanged
        var entryAfterSave = deleteContext.Entry(toDelete);
        entryAfterSave.State.ShouldBe(EntityState.Unchanged);

        // Verify the entity is still in the database
        await using var readContext = CreateContext();
        var raw = await readContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        raw.ShouldNotBeNull();
        raw.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Deleting_auditable_entity_also_sets_UpdatedAt_and_UpdatedBy()
    {
        // Arrange
        var user = User.Create("auditdel", "auditdel@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _dateTimeService.UtcNow.Returns(DeleteTime);
        _currentUserService.UserId.Returns("admin-user");

        // Act
        await using var deleteContext = CreateContext();
        var toDelete = await deleteContext.Users.FindAsync(user.Id);
        toDelete.ShouldNotBeNull();
        deleteContext.Users.Remove(toDelete);
        await deleteContext.SaveChangesAsync();

        // Assert — UpdatedAt/UpdatedBy set during soft delete (because Deleted→Modified)
        await using var readContext = CreateContext();
        var raw = await readContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        raw.ShouldNotBeNull();
        raw.UpdatedAt.ShouldBe(DeleteTime);
        raw.UpdatedBy.ShouldBe("admin-user");
    }

    // ---------------------------------------------------------------
    // Query filter behavior
    // ---------------------------------------------------------------

    [Fact]
    public async Task Soft_deleted_conversation_is_excluded_by_query_filter()
    {
        // Arrange
        var user = User.Create("filterconv", "filterconv@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var conversation = Conversation.Create(user.Id, "Filter Test");
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        _dateTimeService.UtcNow.Returns(DeleteTime);

        await using var deleteContext = CreateContext();
        var toDelete = await deleteContext.Conversations.FindAsync(conversation.Id);
        toDelete.ShouldNotBeNull();
        deleteContext.Conversations.Remove(toDelete);
        await deleteContext.SaveChangesAsync();

        // Act & Assert — LINQ query with filter
        await using var readContext = CreateContext();
        var filtered = await readContext.Conversations
            .Where(c => c.Id == conversation.Id)
            .ToListAsync();
        filtered.ShouldBeEmpty();

        // Act & Assert — IgnoreQueryFilters shows the record
        var unfiltered = await readContext.Conversations
            .IgnoreQueryFilters()
            .Where(c => c.Id == conversation.Id)
            .ToListAsync();
        unfiltered.Count.ShouldBe(1);
        unfiltered[0].IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Soft_deleted_system_prompt_is_excluded_by_query_filter()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Deleted Prompt", "template", "desc");
        _context.SystemPrompts.Add(prompt);
        await _context.SaveChangesAsync();

        _dateTimeService.UtcNow.Returns(DeleteTime);

        await using var deleteContext = CreateContext();
        var toDelete = await deleteContext.SystemPrompts.FindAsync(prompt.Id);
        toDelete.ShouldNotBeNull();
        deleteContext.SystemPrompts.Remove(toDelete);
        await deleteContext.SaveChangesAsync();

        // Act & Assert
        await using var readContext = CreateContext();
        var filtered = await readContext.SystemPrompts
            .Where(sp => sp.Id == prompt.Id)
            .ToListAsync();
        filtered.ShouldBeEmpty();

        var unfiltered = await readContext.SystemPrompts
            .IgnoreQueryFilters()
            .Where(sp => sp.Id == prompt.Id)
            .ToListAsync();
        unfiltered.Count.ShouldBe(1);
        unfiltered[0].IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Soft_deleted_user_is_excluded_by_query_filter()
    {
        // Arrange
        var user = User.Create("filteruser", "filteruser@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _dateTimeService.UtcNow.Returns(DeleteTime);

        await using var deleteContext = CreateContext();
        var toDelete = await deleteContext.Users.FindAsync(user.Id);
        toDelete.ShouldNotBeNull();
        deleteContext.Users.Remove(toDelete);
        await deleteContext.SaveChangesAsync();

        // Act & Assert
        await using var readContext = CreateContext();
        var filtered = await readContext.Users
            .Where(u => u.Id == user.Id)
            .ToListAsync();
        filtered.ShouldBeEmpty();

        var unfiltered = await readContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == user.Id)
            .ToListAsync();
        unfiltered.Count.ShouldBe(1);
        unfiltered[0].IsDeleted.ShouldBeTrue();
    }

    // ---------------------------------------------------------------
    // Non-auditable entities are NOT affected
    // ---------------------------------------------------------------

    [Fact]
    public async Task Deleting_Message_performs_hard_delete()
    {
        // Arrange — Message extends BaseEntity, not BaseAuditableEntity
        var user = User.Create("msgdel", "msgdel@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var conversation = Conversation.Create(user.Id, "Msg delete test");
        conversation.AddMessage(MessageRole.User, "To be hard deleted");
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        var messageId = conversation.Messages.First().Id;

        // Act
        await using var deleteContext = CreateContext();
        var message = await deleteContext.Messages.FindAsync(messageId);
        message.ShouldNotBeNull();
        deleteContext.Messages.Remove(message);
        await deleteContext.SaveChangesAsync();

        // Assert — physically deleted (not soft deleted)
        await using var readContext = CreateContext();
        var deleted = await readContext.Messages.FindAsync(messageId);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task Deleting_AuditEntry_performs_hard_delete()
    {
        // Arrange — AuditEntry extends BaseEntity, not BaseAuditableEntity
        var auditEntry = AuditEntry.Create(
            Guid.NewGuid(),
            "TestAction",
            "TestEntity",
            entityId: Guid.NewGuid().ToString(),
            details: "Hard delete test");

        _context.AuditEntries.Add(auditEntry);
        await _context.SaveChangesAsync();

        var entryId = auditEntry.Id;

        // Act
        await using var deleteContext = CreateContext();
        var entry = await deleteContext.AuditEntries.FindAsync(entryId);
        entry.ShouldNotBeNull();
        deleteContext.AuditEntries.Remove(entry);
        await deleteContext.SaveChangesAsync();

        // Assert — physically deleted
        await using var readContext = CreateContext();
        var deleted = await readContext.AuditEntries.FindAsync(entryId);
        deleted.ShouldBeNull();
    }

    // ---------------------------------------------------------------
    // New entities default to IsDeleted = false
    // ---------------------------------------------------------------

    [Fact]
    public async Task New_auditable_entity_has_IsDeleted_false_and_DeletedAt_null()
    {
        // Arrange & Act
        var user = User.Create("newuser", "newuser@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var raw = await readContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        raw.ShouldNotBeNull();
        raw.IsDeleted.ShouldBeFalse();
        raw.DeletedAt.ShouldBeNull();
    }
}
