namespace nem.Mimir.Domain.Events;

using nem.Mimir.Domain.Common;

public record UserCreatedEvent(Guid UserId, string Username) : IDomainEvent;
