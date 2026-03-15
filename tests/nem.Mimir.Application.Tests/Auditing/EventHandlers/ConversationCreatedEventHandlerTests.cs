using nem.Mimir.Application.Auditing.EventHandlers;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Events;
using nem.Mimir.Domain.ValueObjects;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Auditing.EventHandlers;

public sealed class ConversationCreatedEventHandlerTests
{
    private readonly IAuditService _auditService;
    private readonly ConversationCreatedEventHandler _handler;

    public ConversationCreatedEventHandlerTests()
    {
        _auditService = Substitute.For<IAuditService>();
        _handler = new ConversationCreatedEventHandler(_auditService);
    }

    [Fact]
    public async Task Handle_ShouldLogConversationCreatedEvent()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var notification = new ConversationCreatedEvent(conversationId, userId);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await _auditService.Received(1).LogAsync(
            UserId.From(userId),
            "ConversationCreated",
            "Conversation",
            conversationId.ToString(),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPassCancellationToken()
    {
        // Arrange
        var notification = new ConversationCreatedEvent(Guid.NewGuid(), Guid.NewGuid());
        using var cts = new CancellationTokenSource();

        // Act
        await _handler.Handle(notification, cts.Token);

        // Assert
        await _auditService.Received(1).LogAsync(
            Arg.Any<UserId>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            cts.Token);
    }
}
