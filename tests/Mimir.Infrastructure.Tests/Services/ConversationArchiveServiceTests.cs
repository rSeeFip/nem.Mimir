using Microsoft.EntityFrameworkCore;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using Mimir.Infrastructure.Persistence;
using Mimir.Infrastructure.Services;
using Mimir.Infrastructure.Tests.Persistence.Repositories;
using NSubstitute;
using Shouldly;

namespace Mimir.Infrastructure.Tests.Services;

public sealed class ConversationArchiveServiceTests : RepositoryTestBase
{
    [Fact]
    public async Task ArchiveInactiveConversationsAsync_ArchivesStaleConversations()
    {
        // Arrange
        var dateTimeService = Substitute.For<IDateTimeService>();
        dateTimeService.UtcNow.Returns(new DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero));

        var unitOfWork = new Mimir.Infrastructure.Persistence.UnitOfWork(Context);

        var service = new ConversationArchiveService(Context, dateTimeService, unitOfWork);

        // Create a conversation that was last updated 40 days ago
        var oldConversation = Conversation.Create(Guid.NewGuid(), "Old Conversation");
        Context.Conversations.Add(oldConversation);
        await Context.SaveChangesAsync();


        // Manually set CreatedAt to 40 days ago using SQL to bypass audit interceptor
        await using var updateContext = CreateContext();
        await updateContext.Database.ExecuteSqlAsync(
            $"UPDATE conversations SET created_at = {new DateTimeOffset(2025, 6, 5, 12, 0, 0, TimeSpan.Zero):s}, updated_at = NULL WHERE id = {oldConversation.Id}");

        // Re-create context for the service to pick up the changes
        await using var testContext = CreateContext();
        var testUnitOfWork = new Mimir.Infrastructure.Persistence.UnitOfWork(testContext);
        var testService = new ConversationArchiveService(testContext, dateTimeService, testUnitOfWork);

        // Act
        var archived = await testService.ArchiveInactiveConversationsAsync(30);

        // Assert
        archived.ShouldBe(1);

        await using var verifyContext = CreateContext();
        var result = await verifyContext.Conversations.FindAsync(oldConversation.Id);
        result!.Status.ShouldBe(ConversationStatus.Archived);
    }

    [Fact]
    public async Task ArchiveInactiveConversationsAsync_DoesNotArchiveRecentConversations()
    {
        // Arrange
        var dateTimeService = Substitute.For<IDateTimeService>();
        dateTimeService.UtcNow.Returns(new DateTimeOffset(2025, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var unitOfWork = new Mimir.Infrastructure.Persistence.UnitOfWork(Context);
        var service = new ConversationArchiveService(Context, dateTimeService, unitOfWork);

        // The RepositoryTestBase sets CreatedAt to 2025-06-15 via the audit interceptor
        var recentConversation = Conversation.Create(Guid.NewGuid(), "Recent Conversation");
        Context.Conversations.Add(recentConversation);
        await Context.SaveChangesAsync();

        // Act — 30 day threshold, conversation is only 1 day old
        var archived = await service.ArchiveInactiveConversationsAsync(30);

        // Assert
        archived.ShouldBe(0);
    }

    [Fact]
    public async Task ArchiveInactiveConversationsAsync_ZeroDays_ReturnsZero()
    {
        // Arrange
        var dateTimeService = Substitute.For<IDateTimeService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = new ConversationArchiveService(Context, dateTimeService, unitOfWork);

        // Act
        var archived = await service.ArchiveInactiveConversationsAsync(0);

        // Assert
        archived.ShouldBe(0);
    }

    [Fact]
    public async Task ArchiveInactiveConversationsAsync_NegativeDays_ReturnsZero()
    {
        // Arrange
        var dateTimeService = Substitute.For<IDateTimeService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = new ConversationArchiveService(Context, dateTimeService, unitOfWork);

        // Act
        var archived = await service.ArchiveInactiveConversationsAsync(-5);

        // Assert
        archived.ShouldBe(0);
    }
}
