namespace Mimir.Infrastructure.Tests.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using Mimir.Infrastructure.Persistence;
using Mimir.Infrastructure.Persistence.Repositories;
using Shouldly;

public class ConversationRepositoryTests : RepositoryTestBase
{
    private ConversationRepository CreateRepository() => new(Context);

    private ConversationRepository CreateRepository(MimirDbContext context) => new(context);

    private async Task<User> SeedUserAsync(string username = "convuser")
    {
        var user = User.Create(username, $"{username}@example.com", UserRole.User);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateAsync_adds_conversation_to_context()
    {
        // Arrange
        var user = await SeedUserAsync();
        var conversation = Conversation.Create(user.Id, "Test Conversation");
        var repo = CreateRepository();

        // Act
        var result = await repo.CreateAsync(conversation);
        await Context.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var retrieved = await readContext.Conversations.FindAsync(conversation.Id);
        retrieved.ShouldNotBeNull();
        retrieved.Title.ShouldBe("Test Conversation");
        retrieved.UserId.ShouldBe(user.Id);
        result.Id.ShouldBe(conversation.Id);
    }

    [Fact]
    public async Task GetByIdAsync_returns_conversation_when_exists()
    {
        // Arrange
        var user = await SeedUserAsync();
        var conversation = Conversation.Create(user.Id, "GetById Conv");
        Context.Conversations.Add(conversation);
        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByIdAsync(conversation.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Title.ShouldBe("GetById Conv");
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
    public async Task GetByUserIdAsync_returns_paginated_conversations_ordered_by_updated_at()
    {
        // Arrange
        var user = await SeedUserAsync("paginuser");

        for (var i = 1; i <= 5; i++)
        {
            var conv = Conversation.Create(user.Id, $"Conversation {i}");
            Context.Conversations.Add(conv);
        }

        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByUserIdAsync(user.Id, 1, 3);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(2);
        result.PageNumber.ShouldBe(1);
        result.HasNextPage.ShouldBeTrue();
        result.HasPreviousPage.ShouldBeFalse();
    }

    [Fact]
    public async Task GetByUserIdAsync_returns_second_page()
    {
        // Arrange
        var user = await SeedUserAsync("page2user");

        for (var i = 1; i <= 5; i++)
        {
            var conv = Conversation.Create(user.Id, $"Page2 Conv {i}");
            Context.Conversations.Add(conv);
        }

        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByUserIdAsync(user.Id, 2, 3);

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
    public async Task GetWithMessagesAsync_eagerly_loads_messages()
    {
        // Arrange
        var user = await SeedUserAsync("msguser");
        var conversation = Conversation.Create(user.Id, "With Messages");
        conversation.AddMessage(MessageRole.User, "Hello!");
        conversation.AddMessage(MessageRole.Assistant, "Hi!", "gpt-4");

        Context.Conversations.Add(conversation);
        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetWithMessagesAsync(conversation.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Title.ShouldBe("With Messages");
        result.Messages.Count.ShouldBe(2);
        result.Messages.ShouldContain(m => m.Content == "Hello!");
        result.Messages.ShouldContain(m => m.Content == "Hi!");
    }

    [Fact]
    public async Task GetWithMessagesAsync_returns_null_when_not_found()
    {
        // Arrange
        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetWithMessagesAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_conversation()
    {
        // Arrange
        var user = await SeedUserAsync("deluser");
        var conversation = Conversation.Create(user.Id, "To Delete");
        conversation.AddMessage(MessageRole.User, "Will be deleted");
        conversation.AddMessage(MessageRole.Assistant, "Also deleted");

        Context.Conversations.Add(conversation);
        await Context.SaveChangesAsync();

        var conversationId = conversation.Id;

        // Act
        await using var deleteContext = CreateContext();
        var repo = CreateRepository(deleteContext);
        await repo.DeleteAsync(conversationId);
        await deleteContext.SaveChangesAsync();

        // Assert — conversation is soft-deleted (filtered out by query filter)
        await using var readContext = CreateContext();

        // LINQ queries respect the global query filter — soft-deleted conversation is not returned
        var filtered = await readContext.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        filtered.ShouldBeNull();

        // FindAsync bypasses query filters — we can still see the soft-deleted row
        var raw = await readContext.Conversations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        raw.ShouldNotBeNull();
        raw.IsDeleted.ShouldBeTrue();
        raw.DeletedAt.ShouldNotBeNull();

        // Messages are still present (soft delete doesn't cascade)
        var messages = await readContext.Messages
            .Where(m => m.ConversationId == conversationId)
            .ToListAsync();
        messages.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteAsync_is_noop_when_not_found()
    {
        // Act — should not throw
        var repo = CreateRepository();
        await repo.DeleteAsync(Guid.NewGuid());
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetMessagesAsync_returns_paginated_messages_ordered_by_created_at()
    {
        // Arrange
        var user = await SeedUserAsync("msgpaguser");
        var conversation = Conversation.Create(user.Id, "Msg Pagination");

        for (var i = 1; i <= 5; i++)
        {
            conversation.AddMessage(MessageRole.User, $"Message {i}");
        }

        Context.Conversations.Add(conversation);
        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetMessagesAsync(conversation.Id, 1, 3);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(2);
        result.PageNumber.ShouldBe(1);
    }

    [Fact]
    public async Task GetMessagesAsync_returns_empty_for_unknown_conversation()
    {
        // Arrange
        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetMessagesAsync(Guid.NewGuid(), 1, 10);

        // Assert
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task UpdateAsync_modifies_existing_conversation()
    {
        // Arrange
        var user = await SeedUserAsync("updconv");
        var conversation = Conversation.Create(user.Id, "Original Title");
        Context.Conversations.Add(conversation);
        await Context.SaveChangesAsync();

        // Act
        await using var updateContext = CreateContext();
        var toUpdate = await updateContext.Conversations.FindAsync(conversation.Id);
        toUpdate.ShouldNotBeNull();
        toUpdate.UpdateTitle("Updated Title");

        var repo = CreateRepository(updateContext);
        await repo.UpdateAsync(toUpdate);
        await updateContext.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var retrieved = await readContext.Conversations.FindAsync(conversation.Id);
        retrieved.ShouldNotBeNull();
        retrieved.Title.ShouldBe("Updated Title");
    }
}
