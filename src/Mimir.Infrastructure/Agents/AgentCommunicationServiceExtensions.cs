namespace Mimir.Infrastructure.Agents;

using Microsoft.Extensions.DependencyInjection;

public static class AgentCommunicationServiceExtensions
{
    public static IServiceCollection AddAgentCommunicationBus(this IServiceCollection services)
    {
        services.AddSingleton<IAgentMessagePersistence, AgentMessagePersistence>();
        services.AddSingleton<IAgentCommunicationBus, AgentCommunicationBus>();
        return services;
    }
}
