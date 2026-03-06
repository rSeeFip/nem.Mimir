namespace Mimir.Domain.Agents.Messages;

public sealed record AgentStatusUpdate(
    Guid MessageId,
    string SourceAgentId,
    string? TargetAgentId,
    DateTimeOffset Timestamp,
    AgentMessagePriority Priority,
    string Status,
    string? Details,
    string? Topic = null) : IAgentMessage;
