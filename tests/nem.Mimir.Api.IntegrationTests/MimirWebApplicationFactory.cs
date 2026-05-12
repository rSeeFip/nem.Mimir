using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using nem.Mimir.Infrastructure.Persistence;
using Wolverine;
namespace nem.Mimir.Api.IntegrationTests;

public sealed class MimirWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.RemoveAll<DbContextOptions<MimirDbContext>>();
            services.RemoveAll<MimirDbContext>();

            services.AddDbContext<MimirDbContext>(options =>
            {
                options.UseInMemoryDatabase("MimirTestDb_" + Guid.NewGuid().ToString("N"));
            });

            services.DisableAllExternalWolverineTransports();

            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
            });

            services.AddHealthChecks()
                .AddCheck("test-host", () => HealthCheckResult.Healthy("Integration test host is healthy."));
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
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

internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    internal const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());
}
