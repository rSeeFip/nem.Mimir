using System.Text.Json;
using MediatR;
using Mimir.Application.Common;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Events;

namespace Mimir.Application.Auditing.EventHandlers;

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
