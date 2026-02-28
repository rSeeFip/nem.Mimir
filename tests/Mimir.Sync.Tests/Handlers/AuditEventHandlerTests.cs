using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mimir.Application.Common.Interfaces;
using Mimir.Sync.Handlers;
using Mimir.Sync.Messages;
using NSubstitute;
using Shouldly;

namespace Mimir.Sync.Tests.Handlers;

public sealed class AuditEventHandlerTests
{
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private static AuditEventPublished CreateMessage(
        Guid? userId = null,
        string action = "TestAction",
        string resourceType = "TestResource",
        Guid? resourceId = null,
        string? details = "some details") =>
        new(
            userId ?? Guid.NewGuid(),
            action,
            resourceType,
            resourceId ?? Guid.NewGuid(),
            details,
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_ShouldPassAllFieldsToAuditService()
    {
        var message = CreateMessage(
            userId: Guid.NewGuid(),
            action: "UserLoggedIn",
            resourceType: "Session",
            resourceId: Guid.NewGuid(),
            details: "{\"ip\":\"127.0.0.1\"}");
        var logger = NullLogger<AuditEventHandler>.Instance;

        await AuditEventHandler.Handle(message, _auditService, logger, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            message.UserId,
            "UserLoggedIn",
            "Session",
            message.ResourceId.ToString(),
            "{\"ip\":\"127.0.0.1\"}",
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldForwardNullDetails()
    {
        var message = CreateMessage(details: null);
        var logger = NullLogger<AuditEventHandler>.Instance;

        await AuditEventHandler.Handle(message, _auditService, logger, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            message.UserId,
            message.Action,
            message.ResourceType,
            message.ResourceId.ToString(),
            null,
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldLogDebugMessage()
    {
        var message = CreateMessage();
        var logger = new FakeLogger<AuditEventHandler>();

        await AuditEventHandler.Handle(message, _auditService, logger, CancellationToken.None);

        logger.Entries.ShouldContain(e => e.LogLevel == LogLevel.Debug);
    }
}
