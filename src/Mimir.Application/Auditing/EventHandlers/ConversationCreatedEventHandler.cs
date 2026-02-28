using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Events;

namespace Mimir.Application.Auditing.EventHandlers;

internal sealed class ConversationCreatedEventHandler : INotificationHandler<ConversationCreatedEvent>
{
    private readonly IAuditService _auditService;

    public ConversationCreatedEventHandler(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task Handle(ConversationCreatedEvent notification, CancellationToken cancellationToken)
    {
        await _auditService.LogAsync(
            notification.UserId,
            "ConversationCreated",
            "Conversation",
            notification.ConversationId.ToString(),
            cancellationToken: cancellationToken);
    }
}
