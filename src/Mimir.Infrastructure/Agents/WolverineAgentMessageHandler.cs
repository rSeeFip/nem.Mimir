namespace Mimir.Infrastructure.Agents;

using Mimir.Domain.Agents.Messages;

public sealed class WolverineAgentMessageHandler
{
    public static Task Handle(IAgentMessage message, IAgentCommunicationBus communicationBus, CancellationToken cancellationToken)
    {
        return communicationBus.RouteIncomingAsync(message, cancellationToken);
    }
}
