namespace Mimir.Infrastructure.Agents;

using Mimir.Domain.Agents.Messages;

/// <summary>
/// Persists agent messages to durable storage for audit and replay.
/// </summary>
public interface IAgentMessagePersistence
{
    Task PersistAsync(IAgentMessage message, CancellationToken cancellationToken);
}
