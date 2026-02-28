namespace Mimir.Domain.Events;

using Mimir.Domain.Common;

public record ConversationCreatedEvent(Guid ConversationId, Guid UserId) : IDomainEvent;
