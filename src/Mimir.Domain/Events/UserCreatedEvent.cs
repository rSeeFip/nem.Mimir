namespace Mimir.Domain.Events;

using Mimir.Domain.Common;

public record UserCreatedEvent(Guid UserId, string Username) : IDomainEvent;
