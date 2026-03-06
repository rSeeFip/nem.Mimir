namespace Mimir.Infrastructure.Mcp;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mimir.Domain.Tools;

public static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddMcpClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<List<McpServerConfiguration>>(configuration.GetSection("McpServers"));
        services.AddHttpClient(McpClientManager.HttpClientName);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<McpClientManager>();
        services.AddScoped<IToolProvider, McpToolAdapter>();

        return services;
    }
}
