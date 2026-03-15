namespace nem.Mimir.Domain.Events;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;

public record MessageSentEvent(Guid MessageId, Guid ConversationId, MessageRole Role) : IDomainEvent;
