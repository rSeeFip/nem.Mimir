using Mimir.Application.Auditing.EventHandlers;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Enums;
using Mimir.Domain.Events;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Auditing.EventHandlers;

public sealed class MessageSentEventHandlerTests
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly MessageSentEventHandler _handler;

    public MessageSentEventHandlerTests()
    {
        _auditService = Substitute.For<IAuditService>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new MessageSentEventHandler(_auditService, _currentUserService);
    }

    [Fact]
    public async Task Handle_WithAuthenticatedUser_ShouldLogWithUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var notification = new MessageSentEvent(messageId, conversationId, MessageRole.User);

        _currentUserService.UserId.Returns(userId.ToString());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await _auditService.Received(1).LogAsync(
            userId,
            "MessageSent",
            "Message",
            messageId.ToString(),
            Arg.Is<string>(s => s.Contains("User")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNoCurrentUser_ShouldLogWithEmptyGuid()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var notification = new MessageSentEvent(messageId, conversationId, MessageRole.Assistant);

        _currentUserService.UserId.Returns((string?)null);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await _auditService.Received(1).LogAsync(
            Guid.Empty,
            "MessageSent",
            "Message",
            messageId.ToString(),
            Arg.Is<string>(s => s.Contains("Assistant")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldIncludeRoleInDetails()
    {
        // Arrange
        var notification = new MessageSentEvent(Guid.NewGuid(), Guid.NewGuid(), MessageRole.User);
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await _auditService.Received(1).LogAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("\"Role\":\"User\"")),
            Arg.Any<CancellationToken>());
    }
}
