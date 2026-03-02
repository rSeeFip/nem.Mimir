using System.Text.Json;
using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Events;
using Mimir.Domain.ValueObjects;

namespace Mimir.Application.Auditing.EventHandlers;

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
            ? UserId.From(Guid.Parse(_currentUserService.UserId))
            : UserId.Empty;

        var details = JsonSerializer.Serialize(new { Role = notification.Role.ToString() });

        await _auditService.LogAsync(
            userId,
            "MessageSent",
            "Message",
            notification.MessageId.ToString(),
            details,
            cancellationToken);
    }
}
