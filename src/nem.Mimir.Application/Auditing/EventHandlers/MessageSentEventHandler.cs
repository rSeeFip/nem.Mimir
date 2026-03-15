using System.Text.Json;
using MediatR;
using nem.Mimir.Application.Common;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Events;

namespace nem.Mimir.Application.Auditing.EventHandlers;

internal sealed class MessageSentEventHandler : INotificationHandler<MessageSentEvent>
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public MessageSentEventHandler(IAuditService auditService, ICurrentUserService currentUserService)
    {
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task Handle(MessageSentEvent notification, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId is not null
            ? Guid.Parse(_currentUserService.UserId)
            : Guid.Empty;

        var details = JsonSerializer.Serialize(new { Role = notification.Role.ToString() }, JsonDefaults.Options);

        await _auditService.LogAsync(
            userId,
            "MessageSent",
            "Message",
            notification.MessageId.ToString(),
            details,
            cancellationToken);
    }
}
