using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using nem.Mimir.Infrastructure.Persistence;
using Wolverine;
namespace nem.Mimir.Api.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Replaces external dependencies (DB, Keycloak, LiteLLM) with test-safe alternatives.
/// </summary>
public sealed class MimirWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        host.Start();
        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove the real DbContext registration and use in-memory for integration tests
            services.RemoveAll<DbContextOptions<MimirDbContext>>();
            services.RemoveAll<MimirDbContext>();

            services.AddDbContext<MimirDbContext>(options =>
            {
                options.UseInMemoryDatabase("MimirTestDb_" + Guid.NewGuid().ToString("N"));
            });

            // Disable Wolverine's external transports (RabbitMQ) for integration tests
            services.DisableAllExternalWolverineTransports();
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override health check-related settings to avoid connecting to real services
            var testSettings = new Dictionary<string, string?>
            {
                ["Jwt:Authority"] = "https://localhost",
                ["Jwt:Audience"] = "mimir-api",
                ["Jwt:RequireHttpsMetadata"] = "false",
                ["LiteLlm:BaseUrl"] = "",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=mimir_test",
                ["Database:ConnectionString"] = "Host=localhost;Database=mimir_test"
            };

            config.AddInMemoryCollection(testSettings);
        });
    }
}
