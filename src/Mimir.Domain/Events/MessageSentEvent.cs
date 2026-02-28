namespace Mimir.Domain.Events;

using Mimir.Domain.Common;
using Mimir.Domain.Enums;

public record MessageSentEvent(Guid MessageId, Guid ConversationId, MessageRole Role) : IDomainEvent;
