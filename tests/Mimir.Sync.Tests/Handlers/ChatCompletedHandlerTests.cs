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

public sealed class ChatCompletedHandlerTests
{
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private static ChatCompleted CreateMessage(
        Guid? conversationId = null,
        string model = "qwen-2.5-72b",
        int promptTokens = 100,
        int completionTokens = 50,
        TimeSpan? duration = null) =>
        new(
            conversationId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            model,
            promptTokens,
            completionTokens,
            duration ?? TimeSpan.FromMilliseconds(500),
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_ShouldCallAuditServiceWithCorrectParameters()
    {
        var message = CreateMessage();
        var logger = NullLogger<ChatCompletedHandler>.Instance;

        await ChatCompletedHandler.Handle(message, _auditService, logger, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            UserId.Empty,
            "ChatCompleted",
            "Conversation",
            message.ConversationId.ToString(),
            Arg.Any<string>(),
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldSerializeDetailsAsJson()
    {
        var message = CreateMessage(model: "phi-4-mini", promptTokens: 200, completionTokens: 75, duration: TimeSpan.FromMilliseconds(1234));
        var logger = NullLogger<ChatCompletedHandler>.Instance;

        await ChatCompletedHandler.Handle(message, _auditService, logger, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            Arg.Any<UserId>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(details =>
                details != null &&
                details.Contains("\"Model\":\"phi-4-mini\"") &&
                details.Contains("\"PromptTokens\":200") &&
                details.Contains("\"CompletionTokens\":75") &&
                details.Contains("\"DurationMs\":1234")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLogDebugMessage()
    {
        var message = CreateMessage();
        var logger = new FakeLogger<ChatCompletedHandler>();

        await ChatCompletedHandler.Handle(message, _auditService, logger, CancellationToken.None);

        logger.Entries.ShouldContain(e => e.LogLevel == LogLevel.Debug);
    }

    [Fact]
    public async Task Handle_WithCancellationRequested_ShouldPassTokenToAuditService()
    {
        var message = CreateMessage();
        var logger = NullLogger<ChatCompletedHandler>.Instance;
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        await ChatCompletedHandler.Handle(message, _auditService, logger, token);

        await _auditService.Received(1).LogAsync(
            Arg.Any<UserId>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            token);
    }
}
