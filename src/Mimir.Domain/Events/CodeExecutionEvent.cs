namespace Mimir.Domain.Events;

using Mimir.Domain.Common;

public record CodeExecutionEvent(Guid ConversationId, string Language, int ExitCode, long ExecutionTimeMs, bool TimedOut) : IDomainEvent;
