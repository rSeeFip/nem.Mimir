namespace Mimir.Infrastructure.Agents;

using System.Threading.Channels;
using Mimir.Domain.Agents.Messages;

public interface IAgentCommunicationBus
{
    int PendingCount { get; }

    Task SendDirectAsync(IAgentMessage message, CancellationToken cancellationToken = default);

    Task BroadcastAsync(IAgentMessage message, CancellationToken cancellationToken = default);

    Task PublishToTopicAsync(string topic, IAgentMessage message, CancellationToken cancellationToken = default);

    Task RouteIncomingAsync(IAgentMessage message, CancellationToken cancellationToken = default);

    ChannelReader<IAgentMessage> SubscribeAgent(string agentId, CancellationToken cancellationToken = default);

    ChannelReader<IAgentMessage> SubscribeTopic(string topic, CancellationToken cancellationToken = default);
}
