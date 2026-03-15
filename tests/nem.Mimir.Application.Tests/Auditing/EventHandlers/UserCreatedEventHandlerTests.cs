using nem.Mimir.Application.Auditing.EventHandlers;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Events;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Auditing.EventHandlers;

public sealed class UserCreatedEventHandlerTests
{
    private readonly IAuditService _auditService;
    private readonly UserCreatedEventHandler _handler;

    public UserCreatedEventHandlerTests()
    {
        _auditService = Substitute.For<IAuditService>();
        _handler = new UserCreatedEventHandler(_auditService);
    }

    [Fact]
    public async Task Handle_ShouldLogUserCreatedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var username = "testuser";
        var notification = new UserCreatedEvent(userId, username);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await _auditService.Received(1).LogAsync(
            userId,
            "UserCreated",
            "User",
            userId.ToString(),
            Arg.Is<string>(s => s.Contains("testuser")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldIncludeUsernameInDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notification = new UserCreatedEvent(userId, "admin_user");

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await _auditService.Received(1).LogAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("\"Username\":\"admin_user\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPassCancellationToken()
    {
        // Arrange
        var notification = new UserCreatedEvent(Guid.NewGuid(), "user");
        using var cts = new CancellationTokenSource();

        // Act
        await _handler.Handle(notification, cts.Token);

        // Assert
        await _auditService.Received(1).LogAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            cts.Token);
    }
}
