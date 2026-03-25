using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Events;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Auditing.EventHandlers;

internal sealed class ConversationCreatedEventHandler
{
    private readonly IAuditService _auditService;

    public ConversationCreatedEventHandler(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task Handle(ConversationCreatedEvent notification, CancellationToken cancellationToken)
    {
        await _auditService.LogAsync(
            UserId.From(notification.UserId),
            "ConversationCreated",
            "Conversation",
            notification.ConversationId.ToString(),
            cancellationToken: cancellationToken);
    }
}
