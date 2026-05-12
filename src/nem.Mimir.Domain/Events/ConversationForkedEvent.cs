namespace nem.Mimir.Domain.Events;

using nem.Mimir.Domain.Common;

public sealed record ConversationForkedEvent(
    Guid ConversationId,
    Guid ParentConversationId,
    string ForkReason,
    Guid UserId) : IDomainEvent;
