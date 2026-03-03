using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mimir.Application.Common.Interfaces;
using Mimir.Sync.Handlers;
using Mimir.Sync.Messages;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Mimir.Sync.Tests;

/// <summary>
/// Comprehensive negative tests for Sync layer handlers.
/// Covers audit service failures, invalid message data, and exception propagation.
/// </summary>
public sealed class SyncNegativeTests
{
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    // ══════════════════════════════════════════════════════════════════
    // MessageSentHandler — failure modes
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MessageSentHandler_AuditServiceThrows_PropagatesException()
    {
        // Arrange
        _auditService.LogAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Audit DB unavailable"));

        var message = new MessageSent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "user", DateTimeOffset.UtcNow);
        var logger = NullLogger<MessageSentHandler>.Instance;

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => MessageSentHandler.Handle(message, _auditService, logger, CancellationToken.None));
    }

    [Fact]
    public async Task MessageSentHandler_EmptyGuidUserId_StillCallsAuditService()
    {
        // Arrange — Empty GUID is valid for the handler (domain validation is upstream)
        var message = new MessageSent(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "user", DateTimeOffset.UtcNow);
        var logger = NullLogger<MessageSentHandler>.Instance;

        // Act
        await MessageSentHandler.Handle(message, _auditService, logger, CancellationToken.None);

        // Assert — handler passes through, audit service decides validity
        await _auditService.Received(1).LogAsync(
            Guid.Empty,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageSentHandler_EmptyRole_StillProcesses()
    {
        // Arrange — empty role string should not cause handler to fail
        var message = new MessageSent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "", DateTimeOffset.UtcNow);
        var logger = NullLogger<MessageSentHandler>.Instance;

        // Act
        await MessageSentHandler.Handle(message, _auditService, logger, CancellationToken.None);

        // Assert — handler should still call audit service
        await _auditService.Received(1).LogAsync(
            Arg.Any<Guid>(),
            "MessageSent",
            "Message",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════
    // ChatCompletedHandler — failure modes
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ChatCompletedHandler_AuditServiceThrows_PropagatesException()
    {
        // Arrange
        _auditService.LogAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Audit service timeout"));

        var message = new ChatCompleted(Guid.NewGuid(), Guid.NewGuid(), "gpt-4o", 100, 50, TimeSpan.FromSeconds(1), DateTimeOffset.UtcNow);
        var logger = NullLogger<ChatCompletedHandler>.Instance;

        // Act & Assert
        await Should.ThrowAsync<TimeoutException>(
            () => ChatCompletedHandler.Handle(message, _auditService, logger, CancellationToken.None));
    }

    [Fact]
    public async Task ChatCompletedHandler_ZeroTokenCount_StillProcesses()
    {
        // Arrange
        var message = new ChatCompleted(Guid.NewGuid(), Guid.NewGuid(), "gpt-4o", 0, 0, TimeSpan.Zero, DateTimeOffset.UtcNow);
        var logger = NullLogger<ChatCompletedHandler>.Instance;

        // Act
        await ChatCompletedHandler.Handle(message, _auditService, logger, CancellationToken.None);

        // Assert
        await _auditService.Received(1).LogAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChatCompletedHandler_NegativeTokenCount_StillProcesses()
    {
        // Arrange — handler should not validate token count
        var message = new ChatCompleted(Guid.NewGuid(), Guid.NewGuid(), "gpt-4o", -1, -1, TimeSpan.FromSeconds(-1), DateTimeOffset.UtcNow);
        var logger = NullLogger<ChatCompletedHandler>.Instance;

        // Act
        await ChatCompletedHandler.Handle(message, _auditService, logger, CancellationToken.None);

        // Assert — handler passes through
        await _auditService.Received(1).LogAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════
    // AuditEventHandler — failure modes
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AuditEventHandler_AuditServiceThrows_PropagatesException()
    {
        // Arrange
        _auditService.LogAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        var message = new AuditEventPublished(Guid.NewGuid(), "UserLogin", "User", Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        var logger = NullLogger<AuditEventHandler>.Instance;

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => AuditEventHandler.Handle(message, _auditService, logger, CancellationToken.None));
    }

    [Fact]
    public async Task AuditEventHandler_NullEntityIdAndDetails_StillProcesses()
    {
        // Arrange
        var message = new AuditEventPublished(Guid.NewGuid(), "Logout", "User", Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        var logger = NullLogger<AuditEventHandler>.Instance;

        // Act
        await AuditEventHandler.Handle(message, _auditService, logger, CancellationToken.None);

        // Assert
        await _auditService.Received(1).LogAsync(
            Arg.Any<Guid>(),
            "Logout",
            "User",
            null,
            null,
            Arg.Any<CancellationToken>());
    }
}
