namespace nem.Mimir.Domain.Agents.Messages;

public sealed record AgentEscalationRequest(
    Guid MessageId,
    string SourceAgentId,
    string TargetAgentId,
    DateTimeOffset Timestamp,
    AgentMessagePriority Priority,
    string TaskId,
    string Reason,
    string RequestedCapability,
    string? Topic = null) : IAgentMessage;
