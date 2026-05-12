using System.Text.Json;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Events;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Auditing.EventHandlers;

internal sealed class UserCreatedEventHandler
{
    private readonly IAuditService _auditService;

    public UserCreatedEventHandler(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(new { Username = notification.Username });

        await _auditService.LogAsync(
            UserId.From(notification.UserId),
            "UserCreated",
            "User",
            notification.UserId.ToString(),
            details,
            cancellationToken);
    }
}
