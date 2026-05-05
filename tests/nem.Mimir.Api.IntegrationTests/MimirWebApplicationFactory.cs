using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using nem.Mimir.Infrastructure.Persistence;
using nem.Contracts.Identity;
using nem.Contracts.Organism;
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

            // Remove background infrastructure that starts against production organism/MCP paths
            // during host startup. Integration tests only need the HTTP surface.
            RemoveFactoryHostedServices(services);
            RemoveServiceByTypeName(services, "nem.Mimir.Infrastructure.Health.MimirHealthReportEmitter");
            RemoveServiceByTypeName(services, "nem.Mimir.Infrastructure.McpServers.McpClientStartupService");

            // Register test-safe organism services so MAPE-K startup does not require
            // the production homeostasis/autonomy infrastructure.
            services.AddSingleton<IHomeostasisAgent, TestHomeostasisAgent>();
            services.AddSingleton<IAutonomyGate, TestAutonomyGate>();
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

    private static void RemoveFactoryHostedServices(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationFactory is not null)
            {
                services.RemoveAt(i);
            }
        }
    }

    private static void RemoveServiceByTypeName(IServiceCollection services, string fullTypeName)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            var serviceTypeName = descriptor.ServiceType.FullName;
            var implementationTypeName = descriptor.ImplementationType?.FullName;

            if (serviceTypeName == fullTypeName || implementationTypeName == fullTypeName)
            {
                services.RemoveAt(i);
            }
        }
    }

    private sealed class TestHomeostasisAgent : IHomeostasisAgent
    {
        public Task<OrganismHealth> MonitorHealthAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new OrganismHealth(
                Timestamp: DateTimeOffset.UtcNow,
                AggregateHealthScore: 1.0,
                ServiceHealthScores: new Dictionary<string, double>
                {
                    ["nem.Mimir"] = 1.0,
                },
                Diagnostics: new Dictionary<string, string>
                {
                    ["status"] = "testing",
                }));
        }

        public Task<HomeostasisCorrectionId> TriggerCorrectionAsync(
            string reason,
            IReadOnlyDictionary<string, double> metrics,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            ArgumentNullException.ThrowIfNull(metrics);

            return Task.FromResult(HomeostasisCorrectionId.New());
        }

        public Task<string> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("testing");
        }
    }

    private sealed class TestAutonomyGate : IAutonomyGate
    {
        public Task<AutonomyLevel> GetAutonomyLevelAsync(
            string serviceName,
            string action,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
            ArgumentException.ThrowIfNullOrWhiteSpace(action);

            return Task.FromResult(AutonomyLevel.L1_Suggest);
        }

        public Task<bool> RequestEscalationAsync(
            string serviceName,
            AutonomyLevel requestedLevel,
            string reason,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            return Task.FromResult(true);
        }

        public Task OverrideAsync(
            string serviceName,
            string action,
            AutonomyLevel level,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
            ArgumentException.ThrowIfNullOrWhiteSpace(action);

            return Task.CompletedTask;
        }
    }
}
