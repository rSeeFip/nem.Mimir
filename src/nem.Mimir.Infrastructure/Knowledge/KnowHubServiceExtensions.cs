namespace nem.Mimir.Infrastructure.Knowledge;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Application.Knowledge;
using nem.KnowHub.Enhancement.Abstractions.Extensions;
using nem.KnowHub.Enhancement.Distillation.Extensions;
using nem.KnowHub.Enhancement.Graph.Extensions;
using nem.KnowHub.Enhancement.GraphRag.Extensions;

public static class KnowHubServiceExtensions
{
    public static IServiceCollection AddKnowHubIntegration(this IServiceCollection services, IConfiguration config)
    {
        var enabled = config.GetValue<bool?>("KnowHub:Enabled") ?? false;
        if (!enabled)
        {
            services.AddSingleton<IKnowHubBridge, NoopKnowHubBridge>();
            services.AddHealthChecks()
                .AddCheck<KnowHubBridgeHealthCheck>("knowhub_bridge");
            return services;
        }

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
