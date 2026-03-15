using Microsoft.Extensions.DependencyInjection;
using nem.Contracts.Agents;

namespace nem.Mimir.Application.Agents;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddAgentOrchestration(this IServiceCollection services)
    {
        services.AddScoped<AgentDispatcher>();
        services.AddScoped<AgentCoordinator>();
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

        return services;
    }
}
