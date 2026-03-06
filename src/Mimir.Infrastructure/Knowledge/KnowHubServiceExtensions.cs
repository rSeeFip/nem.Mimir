namespace Mimir.Infrastructure.Knowledge;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mimir.Application.Knowledge;
using nem.Cognitive.Abstractions.Extensions;
using nem.Cognitive.Distillation.Extensions;
using nem.Cognitive.Graph.Extensions;
using nem.Cognitive.GraphRag.Extensions;

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
