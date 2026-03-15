namespace nem.Mimir.Infrastructure.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.ValueObjects;
using nem.Mimir.Infrastructure.Persistence;
using nem.Mimir.Infrastructure.Persistence.Converters;
using nem.Mimir.Infrastructure.Persistence.Interceptors;
using NSubstitute;
using Shouldly;
using Testcontainers.PostgreSql;

public class MimirDbContextTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private MimirDbContext _context = null!;
    private ICurrentUserService _currentUserService = null!;
    private IDateTimeService _dateTimeService = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.UserId.Returns("test-user-id");
        _currentUserService.IsAuthenticated.Returns(true);

        _dateTimeService = Substitute.For<IDateTimeService>();
        _dateTimeService.UtcNow.Returns(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));

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

    [Fact]
    public async Task Can_create_and_retrieve_user()
    {
        // Arrange
        var user = User.Create("testuser", "test@example.com", UserRole.User);

        // Act
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var retrieved = await readContext.Users.FindAsync(user.Id);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved.Username.ShouldBe("testuser");
        retrieved.Email.ShouldBe("test@example.com");
        retrieved.Role.ShouldBe(UserRole.User);
    }

    [Fact]
    public async Task Can_create_and_retrieve_conversation_with_messages()
    {
        // Arrange
        var user = User.Create("convuser", "conv@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var conversation = Conversation.Create(user.Id, "Test Conversation");
        conversation.AddMessage(MessageRole.User, "Hello!");
        conversation.AddMessage(MessageRole.Assistant, "Hi there!", "gpt-4");

        // Act
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var retrieved = await readContext.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversation.Id);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved.Title.ShouldBe("Test Conversation");
        retrieved.Status.ShouldBe(ConversationStatus.Active);
        retrieved.UserId.ShouldBe(user.Id);
        retrieved.Messages.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Can_create_and_retrieve_audit_entry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var auditEntry = AuditEntry.Create(
            userId,
            "Login",
            "User",
            entityId: userId.ToString(),
            details: "User logged in");

        // Act
        _context.AuditEntries.Add(auditEntry);
        await _context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var retrieved = await readContext.AuditEntries.FindAsync(auditEntry.Id);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved.UserId.ShouldBe(userId);
        retrieved.Action.ShouldBe("Login");
        retrieved.EntityType.ShouldBe("User");
        retrieved.Details.ShouldBe("User logged in");
    }

    [Fact]
    public async Task Audit_interceptor_sets_created_fields_on_add()
    {
        // Arrange
        var user = User.Create("audituser", "audit@example.com", UserRole.Admin);

        // Act
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var retrieved = await readContext.Users.FindAsync(user.Id);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved.CreatedAt.ShouldBe(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));
        retrieved.CreatedBy.ShouldBe("test-user-id");
        retrieved.UpdatedAt.ShouldNotBeNull();
        retrieved.UpdatedBy.ShouldBe("test-user-id");
    }

    [Fact]
    public async Task Audit_interceptor_sets_updated_fields_on_modify()
    {
        // Arrange
        var user = User.Create("moduser", "mod@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Change the time for the update
        var updateTime = new DateTimeOffset(2025, 7, 1, 10, 0, 0, TimeSpan.Zero);
        _dateTimeService.UtcNow.Returns(updateTime);
        _currentUserService.UserId.Returns("admin-user-id");

        // Act — create a fresh context with updated mocks
        await using var updateContext = CreateContext();
        var userToUpdate = await updateContext.Users.FindAsync(user.Id);
        userToUpdate.ShouldNotBeNull();
        userToUpdate.ChangeRole(UserRole.Admin);
        await updateContext.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var retrieved = await readContext.Users.FindAsync(user.Id);
        retrieved.ShouldNotBeNull();
        retrieved.Role.ShouldBe(UserRole.Admin);
        retrieved.UpdatedAt.ShouldBe(updateTime);
        retrieved.UpdatedBy.ShouldBe("admin-user-id");
    }

    [Fact]
    public async Task Value_converter_round_trips_UserId()
    {
        // Arrange
        var originalId = UserId.New();
        var converter = new UserIdConverter();

        // Act
        var toProvider = converter.ConvertToProvider(originalId);
        var fromProvider = converter.ConvertFromProvider(toProvider!);

        // Assert
        toProvider.ShouldBeOfType<Guid>();
        fromProvider.ShouldBe(originalId);
        ((UserId)fromProvider!).Value.ShouldBe(originalId.Value);
    }

    [Fact]
    public async Task Value_converter_round_trips_ConversationId()
    {
        // Arrange
        var originalId = ConversationId.New();
        var converter = new ConversationIdConverter();

        // Act
        var toProvider = converter.ConvertToProvider(originalId);
        var fromProvider = converter.ConvertFromProvider(toProvider!);

        // Assert
        toProvider.ShouldBeOfType<Guid>();
        fromProvider.ShouldBe(originalId);
        ((ConversationId)fromProvider!).Value.ShouldBe(originalId.Value);
    }

    [Fact]
    public async Task Value_converter_round_trips_MessageId()
    {
        // Arrange
        var originalId = MessageId.New();
        var converter = new MessageIdConverter();

        // Act
        var toProvider = converter.ConvertToProvider(originalId);
        var fromProvider = converter.ConvertFromProvider(toProvider!);

        // Assert
        toProvider.ShouldBeOfType<Guid>();
        fromProvider.ShouldBe(originalId);
        ((MessageId)fromProvider!).Value.ShouldBe(originalId.Value);
    }

    [Fact]
    public async Task Enum_stored_as_string_in_database()
    {
        // Arrange
        var user = User.Create("enumuser", "enum@example.com", UserRole.Admin);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act — query raw to verify string storage
        await using var connection = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT role FROM users WHERE id = '{user.Id}'";
        var result = await cmd.ExecuteScalarAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<string>();
        ((string)result).ShouldBe("Admin");
    }

    [Fact]
    public async Task Message_content_stored_as_text_type()
    {
        // Arrange
        var user = User.Create("msguser", "msg@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var conversation = Conversation.Create(user.Id, "Text type test");
        var longContent = new string('x', 10000);
        conversation.AddMessage(MessageRole.User, longContent);
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        // Act
        await using var readContext = CreateContext();
        var message = await readContext.Messages
            .FirstOrDefaultAsync(m => m.ConversationId == conversation.Id);

        // Assert
        message.ShouldNotBeNull();
        message.Content.Length.ShouldBe(10000);
    }

    [Fact]
    public async Task User_email_unique_constraint_enforced()
    {
        // Arrange
        var user1 = User.Create("user1", "duplicate@example.com", UserRole.User);
        var user2 = User.Create("user2", "duplicate@example.com", UserRole.User);

        _context.Users.Add(user1);
        await _context.SaveChangesAsync();

        // Act & Assert
        await using var secondContext = CreateContext();
        secondContext.Users.Add(user2);
        await Should.ThrowAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Conversation_soft_delete_marks_as_deleted()
    {
        // Arrange
        var user = User.Create("cascadeuser", "cascade@example.com", UserRole.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var conversation = Conversation.Create(user.Id, "Cascade test");
        conversation.AddMessage(MessageRole.User, "Will be deleted");
        conversation.AddMessage(MessageRole.Assistant, "Also deleted");
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        var conversationId = conversation.Id;

        // Act
        await using var deleteContext = CreateContext();
        var toDelete = await deleteContext.Conversations.FindAsync(conversationId);
        toDelete.ShouldNotBeNull();
        deleteContext.Conversations.Remove(toDelete);
        await deleteContext.SaveChangesAsync();

        // Assert — soft-deleted conversation is filtered out by global query filter
        await using var readContext = CreateContext();
        var filtered = await readContext.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        filtered.ShouldBeNull();

        // But the row still exists in the database with IsDeleted = true
        var raw = await readContext.Conversations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        raw.ShouldNotBeNull();
        raw.IsDeleted.ShouldBeTrue();
        raw.DeletedAt.ShouldNotBeNull();

        // Messages are still present (soft delete doesn't cascade physical delete)
        var messages = await readContext.Messages
            .Where(m => m.ConversationId == conversationId)
            .ToListAsync();
        messages.Count.ShouldBe(2);
    }
}
