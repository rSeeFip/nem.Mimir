namespace nem.Mimir.Domain.Agents.Messages;

public sealed record AgentTaskResult(
    Guid MessageId,
    string SourceAgentId,
    string TargetAgentId,
    DateTimeOffset Timestamp,
    AgentMessagePriority Priority,
    string TaskId,
    bool Succeeded,
    string? ResultSummary,
    string? Error,
    string? Topic = null) : IAgentMessage;
