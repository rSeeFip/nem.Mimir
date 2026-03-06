namespace Mimir.Domain.Agents.Messages;

public sealed record AgentTaskRequest(
    Guid MessageId,
    string SourceAgentId,
    string TargetAgentId,
    DateTimeOffset Timestamp,
    AgentMessagePriority Priority,
    string TaskId,
    string TaskDescription,
    IReadOnlyDictionary<string, string>? Context = null,
    string? Topic = null) : IAgentMessage;
