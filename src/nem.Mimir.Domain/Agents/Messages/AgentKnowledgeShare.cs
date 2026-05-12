namespace nem.Mimir.Domain.Agents.Messages;

public sealed record AgentKnowledgeShare(
    Guid MessageId,
    string SourceAgentId,
    string? TargetAgentId,
    DateTimeOffset Timestamp,
    AgentMessagePriority Priority,
    string KnowledgeType,
    string Content,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? Topic = null) : IAgentMessage;
