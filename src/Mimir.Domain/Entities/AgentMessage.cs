namespace Mimir.Domain.Entities;

using Mimir.Domain.Common;
using Mimir.Domain.Agents.Messages;

public sealed class AgentMessage : BaseEntity<Guid>
{
    public string MessageType { get; private set; } = string.Empty;
    public string SourceAgentId { get; private set; } = string.Empty;
    public string? TargetAgentId { get; private set; }
    public string? Topic { get; private set; }
    public int Priority { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string Payload { get; private set; } = string.Empty;

    private AgentMessage()
    {
    }

    public static AgentMessage Create(IAgentMessage message, string payload)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload cannot be empty.", nameof(payload));
        }

        return new AgentMessage
        {
            Id = message.MessageId,
            MessageType = message.GetType().Name,
            SourceAgentId = message.SourceAgentId,
            TargetAgentId = message.TargetAgentId,
            Topic = message.Topic,
            Priority = (int)message.Priority,
            Timestamp = message.Timestamp,
            Payload = payload,
        };
    }
}
