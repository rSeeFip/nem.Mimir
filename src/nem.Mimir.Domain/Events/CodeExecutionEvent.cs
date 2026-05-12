namespace nem.Mimir.Domain.Events;

using nem.Mimir.Domain.Common;

public record CodeExecutionEvent(Guid ConversationId, string Language, int ExitCode, long ExecutionTimeMs, bool TimedOut) : IDomainEvent;
