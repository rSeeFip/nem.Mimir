namespace nem.Mimir.Domain.Agents.Messages;

public interface IAgentMessage
{
    Guid MessageId { get; }

    string SourceAgentId { get; }

    string? TargetAgentId { get; }

    DateTimeOffset Timestamp { get; }

    AgentMessagePriority Priority { get; }

    string? Topic { get; }
}

public enum AgentMessagePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3,
}
