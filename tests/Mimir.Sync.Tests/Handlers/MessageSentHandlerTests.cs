using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.ValueObjects;
using Mimir.Sync.Handlers;
using Mimir.Sync.Messages;
using NSubstitute;
using Shouldly;

namespace Mimir.Sync.Tests.Handlers;

public sealed class MessageSentHandlerTests
{
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private static MessageSent CreateMessage(
        Guid? conversationId = null,
        Guid? messageId = null,
        Guid? userId = null,
        string role = "user") =>
        new(
            conversationId ?? Guid.NewGuid(),
            messageId ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            role,
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_ShouldCallAuditServiceWithMessageSentAction()
    {
        var message = CreateMessage();
        var logger = NullLogger<MessageSentHandler>.Instance;

        await MessageSentHandler.Handle(message, _auditService, logger, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            UserId.From(message.UserId),
            "MessageSent",
            "Message",
            message.MessageId.ToString(),
            Arg.Any<string>(),
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldSerializeRoleAndConversationIdInDetails()
    {
        var conversationId = Guid.NewGuid();
        var message = CreateMessage(conversationId: conversationId, role: "assistant");
        var logger = NullLogger<MessageSentHandler>.Instance;

        await MessageSentHandler.Handle(message, _auditService, logger, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            Arg.Any<UserId>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(details =>
                details != null &&
                details.Contains("\"Role\":\"assistant\"") &&
                details.Contains(conversationId.ToString())),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUseCorrectUserId()
    {
        var userId = Guid.NewGuid();
        var message = CreateMessage(userId: userId);
        var logger = NullLogger<MessageSentHandler>.Instance;

        await MessageSentHandler.Handle(message, _auditService, logger, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            UserId.From(userId),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLogDebugMessage()
    {
        var message = CreateMessage();
        var logger = new FakeLogger<MessageSentHandler>();

        await MessageSentHandler.Handle(message, _auditService, logger, CancellationToken.None);

        logger.Entries.ShouldContain(e => e.LogLevel == LogLevel.Debug);
    }
}
