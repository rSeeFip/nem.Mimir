namespace Mimir.Infrastructure.Agents;

using Mimir.Domain.Agents.Messages;

public interface IAgentMessagePersistence
{
    Task PersistAsync(IAgentMessage message, CancellationToken cancellationToken);
}
