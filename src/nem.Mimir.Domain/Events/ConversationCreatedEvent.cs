namespace nem.Mimir.Domain.Events;

using nem.Mimir.Domain.Common;

public record ConversationCreatedEvent(Guid ConversationId, Guid UserId) : IDomainEvent;
