namespace nem.Mimir.Infrastructure.Knowledge;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Application.Knowledge;
using nem.KnowHub.Abstractions.Extensions;
using nem.KnowHub.Distillation.Extensions;
using nem.KnowHub.Graph.Extensions;
using nem.KnowHub.GraphRag.Extensions;

public static class KnowHubServiceExtensions
{
    public static IServiceCollection AddKnowHubIntegration(this IServiceCollection services, IConfiguration config)
    {
        services.AddNemCognitive(config);
        services.AddNemGraphStore(config);
        services.AddNemDistillation(config);
        services.AddNemGraphRag(config);

        services.AddSingleton<IKnowHubBridge, KnowHubBridge>();

        services.AddHealthChecks()
            .AddCheck<KnowHubBridgeHealthCheck>("knowhub_bridge");

        return services;
    }
}
