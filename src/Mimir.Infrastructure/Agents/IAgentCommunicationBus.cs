namespace Mimir.Infrastructure.Agents;

using System.Threading.Channels;
using Mimir.Domain.Agents.Messages;

/// <summary>
/// Messaging bus for inter-agent communication supporting direct, broadcast, and topic-based delivery.
/// </summary>
public interface IAgentCommunicationBus
{
    /// <summary>Gets the number of undelivered messages across all channels.</summary>
    int PendingCount { get; }

    /// <summary>Sends a message directly to the target agent specified in the message.</summary>
    Task SendDirectAsync(IAgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts a message to all subscribed agents.</summary>
    Task BroadcastAsync(IAgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>Publishes a message to all subscribers of the specified topic.</summary>
    Task PublishToTopicAsync(string topic, IAgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>Routes an incoming message to the appropriate agent or topic channel.</summary>
    Task RouteIncomingAsync(IAgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>Subscribes to messages addressed to the specified agent, returning a channel reader.</summary>
    ChannelReader<IAgentMessage> SubscribeAgent(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Subscribes to messages published on the specified topic, returning a channel reader.</summary>
    ChannelReader<IAgentMessage> SubscribeTopic(string topic, CancellationToken cancellationToken = default);
}
