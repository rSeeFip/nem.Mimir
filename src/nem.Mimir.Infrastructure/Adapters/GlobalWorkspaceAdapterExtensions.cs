namespace nem.Mimir.Infrastructure.Adapters;

using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class GlobalWorkspaceAdapterExtensions
{
    public static IServiceCollection AddGlobalWorkspaceAdapter(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(typeof(GlobalWorkspaceAdapterExtensions).Assembly);

        services.Configure<GlobalWorkspaceAdapterOptions>(configuration.GetSection(GlobalWorkspaceAdapterOptions.SectionName));

        services.AddScoped<INotificationHandler<WorkspaceBroadcastNotification>, WorkspaceBroadcastNotificationHandler>();
        services.AddScoped<INotificationHandler<MimirWorkspaceResponseNotification>, MimirWorkspaceResponseHandler>();

        return services;
    }
}
