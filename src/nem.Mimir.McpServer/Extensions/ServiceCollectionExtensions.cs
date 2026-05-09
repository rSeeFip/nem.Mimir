using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace nem.Mimir.McpServer.Extensions;

/// <summary>
/// Extension methods for registering the Mimir MCP server in the DI container and endpoint routing.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Mimir MCP server services to the service collection.
    /// Registers MCP tools, resources, and prompts from this assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNemMcpServer(this IServiceCollection services)
    {
        services
            .AddMcpServer()
            .WithToolsFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .WithPromptsFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .WithResourcesFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .WithHttpTransport();

        return services;
    }

    /// <summary>
    /// Maps the Mimir MCP server HTTP endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">Optional route pattern (defaults to "/mcp").</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapNemMcpServer(this IEndpointRouteBuilder endpoints, string pattern = "/mcp")
    {
        endpoints.MapMcp(pattern);
        return endpoints;
    }
}
