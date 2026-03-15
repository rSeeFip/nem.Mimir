using System.Text.Json;
using MediatR;
using nem.Mimir.Application.Common;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Events;

namespace nem.Mimir.Application.Auditing.EventHandlers;

internal sealed class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly IAuditService _auditService;

    public UserCreatedEventHandler(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(new { Username = notification.Username }, JsonDefaults.Options);

        await _auditService.LogAsync(
            notification.UserId,
            "UserCreated",
            "User",
            notification.UserId.ToString(),
            details,
            cancellationToken);
    }
}
