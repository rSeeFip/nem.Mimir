namespace nem.Mimir.Infrastructure.Adapters;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class GlobalWorkspaceAdapterExtensions
{
    public static IServiceCollection AddGlobalWorkspaceAdapter(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GlobalWorkspaceAdapterOptions>(configuration.GetSection(GlobalWorkspaceAdapterOptions.SectionName));

        return services;
    }
}
