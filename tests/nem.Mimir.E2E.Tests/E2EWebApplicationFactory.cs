using System.Net.Sockets;
using System.Text;
using Npgsql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using nem.Mimir.E2E.Tests.Helpers;
using nem.Mimir.Infrastructure.Persistence;
using nem.Contracts.Identity;
using nem.Contracts.Organism;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Wolverine;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.E2E.Tests;

/// <summary>
/// Custom WebApplicationFactory for end-to-end testing.
/// Uses real PostgreSQL (Testcontainers), real RabbitMQ (Testcontainers), and WireMock for LiteLLM proxy.
/// </summary>
public sealed class E2EWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder("rabbitmq:3-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private WireMockServer _wireMock = null!;

    /// <summary>
    /// Gets the WireMock server for configuring LiteLLM responses in individual tests.
    /// </summary>
    public WireMockServer WireMock => _wireMock;

    /// <summary>
    /// Gets the PostgreSQL connection string for direct DB access in tests.
    /// </summary>
    public string PostgresConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        // 1. Start infrastructure containers
        await _postgres.StartAsync();
        await _rabbitmq.StartAsync();

        await WaitForPostgresAsync();
        await WaitForRabbitMqAsync();

        // 2. Start WireMock for LiteLLM proxy simulation
        _wireMock = WireMockServer.Start();
        ConfigureDefaultWireMockStubs();

        // 3. Apply EF Core migrations to the real database
        await ApplyMigrationsAsync();

        // 4. Force server creation — WebApplicationFactory.Server is lazy,
        //    so we must access it to trigger ConfigureWebHost and full app startup.
        //    Without this, CreateClient() in tests throws:
        //    "Server hasn't been initialized yet"
        await EnsureServerInitializedAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        _wireMock?.Stop();
        _wireMock?.Dispose();
        await _rabbitmq.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var testSettings = new Dictionary<string, string?>
            {
                // Real PostgreSQL from Testcontainers
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Database:ConnectionString"] = _postgres.GetConnectionString(),

                // Real RabbitMQ from Testcontainers
                ["RabbitMQ:ConnectionString"] = _rabbitmq.GetConnectionString(),

                // WireMock for LiteLLM proxy
                ["LiteLlm:BaseUrl"] = _wireMock.Url!,
                ["LiteLlm:ApiKey"] = "test-api-key",
                ["LiteLlm:TimeoutSeconds"] = "30",

                // JWT test settings — use symmetric key for test token validation
                ["MimirApiOptions:StandaloneMode"] = "true",
                ["Jwt:Authority"] = JwtTokenHelper.TestIssuer,
                ["Jwt:Audience"] = JwtTokenHelper.TestAudience,
                ["Jwt:RequireHttpsMetadata"] = "false",

                // Disable OpenBao background integration during test startup.
                // The E2E factory injects all required test configuration directly.
                ["OpenBao:Enabled"] = "false",
                ["OpenBao:Address"] = "http://127.0.0.1:8200",
                ["OpenBao:Token"] = "test-token",
            };

            config.AddInMemoryCollection(testSettings);
        });

        builder.ConfigureTestServices(services =>
        {
            // Disable Wolverine's external transports (RabbitMQ) to prevent
            // connection failures during test server startup.
            // This is the proven pattern from nem.Mimir.Api.IntegrationTests.
            services.DisableAllExternalWolverineTransports();

            // Remove factory-registered hosted services that resolve singleton background
            // workers from the root provider. In tests we only need the HTTP app surface,
            // not long-running telemetry/config sync loops.
            RemoveFactoryHostedServices(services);
            RemoveServiceByTypeName(services, "nem.Mimir.Infrastructure.Health.MimirHealthReportEmitter");

            // Override the LiteLLM named HttpClient to point to WireMock.
            // The original AddHttpClient("LiteLlm", ...) in DependencyInjection.cs
            // captures BaseAddress from config at registration time. This additional
            // configuration delegate runs after the original and overrides BaseAddress.
            // We also reconfigure the resilience handler to avoid retries/circuit-breaker
            // in tests, which can cause flaky failures when WireMock has transient delays.
#pragma warning disable EXTEXP0001 // Experimental API
            services.AddHttpClient("LiteLlm", client =>
            {
                client.BaseAddress = new Uri(_wireMock.Url!);
            })
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

            // Register IDateTimeService — it's required by AuditableEntityInterceptor
            // but has no implementation registered in the main app's DI container.
            services.AddSingleton<IDateTimeService, TestDateTimeService>();
            services.AddSingleton<IHomeostasisAgent, TestHomeostasisAgent>();
            services.AddSingleton<IAutonomyGate, TestAutonomyGate>();
            services.RemoveAll<IExceptionHandler>();
            services.AddExceptionHandler<VerboseTestExceptionHandler>();

            // Override health checks: clear the original registrations (which captured
            // wrong URLs at startup) and add new ones pointing at our test infrastructure.
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
            });
            services.AddHealthChecks()
                .AddNpgSql(_postgres.GetConnectionString(), name: "database")
                .AddUrlGroup(
                    new Uri(_wireMock.Url + "/health"),
                    name: "litellm",
                    timeout: TimeSpan.FromSeconds(5));
            // Override JWT Bearer to use symmetric key validation instead of Keycloak OIDC discovery
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var key = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(JwtTokenHelper.TestSigningKey));

                options.Authority = null;
                options.RequireHttpsMetadata = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = JwtTokenHelper.TestIssuer,
                    ValidateAudience = true,
                    ValidAudience = JwtTokenHelper.TestAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                };
            });
        });
    }

    /// <summary>
    /// Creates an HttpClient with a valid JWT Bearer token pre-configured.
    /// </summary>
    /// <param name="userId">Optional user ID for the token.</param>
    /// <param name="email">Optional email for the token.</param>
    /// <returns>An HttpClient with Authorization header set.</returns>
    public HttpClient CreateAuthenticatedClient(string? userId = null, string? email = null)
    {
        var client = CreateClient();
        var token = JwtTokenHelper.GenerateToken(userId, email);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private void ConfigureDefaultWireMockStubs()
    {
        // POST /v1/chat/completions — non-streaming JSON response.
        // PluginToolProvider always returns at least CodeRunnerPlugin, so
        // SendMessageCommandHandler always takes the tool-loop path (non-streaming).
        // WireMock must return a JSON ChatCompletionResponse, not SSE.
        _wireMock
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                        "id": "chatcmpl-default",
                        "object": "chat.completion",
                        "model": "qwen-2.5-72b",
                        "choices": [{
                            "index": 0,
                            "message": { "role": "assistant", "content": "Hello from Mimir" },
                            "finish_reason": "stop"
                        }],
                        "usage": { "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15 }
                    }
                    """));

        // GET /v1/models — model list
        _wireMock
            .Given(Request.Create()
                .WithPath("/v1/models")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                        "object": "list",
                        "data": [
                            {
                                "id": "qwen-2.5-72b",
                                "object": "model",
                                "created": 1234567890,
                                "owned_by": "litellm"
                            },
                            {
                                "id": "gpt-4o",
                                "object": "model",
                                "created": 1234567890,
                                "owned_by": "litellm"
                            }
                        ]
                    }
                    """));

        // GET /health — LiteLLM health check
        _wireMock
            .Given(Request.Create()
                .WithPath("/health")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"status\":\"healthy\"}"));
    }

    private async Task ApplyMigrationsAsync()
    {
        // Build a temporary service provider to run migrations
        var options = new DbContextOptionsBuilder<MimirDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(MimirDbContext).Assembly.FullName);
            })
            .Options;

        await using var context = new MimirDbContext(options);
        await context.Database.EnsureCreatedAsync();
    }

    private async Task WaitForPostgresAsync()
    {
        await WaitForDependencyAsync(
            "PostgreSQL",
            async cancellationToken =>
            {
                await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync(cancellationToken);
            });
    }

    private async Task WaitForRabbitMqAsync()
    {
        await WaitForDependencyAsync(
            "RabbitMQ",
            async cancellationToken =>
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(_rabbitmq.Hostname, _rabbitmq.GetMappedPublicPort(5672), cancellationToken);
            });
    }

    private async Task EnsureServerInitializedAsync()
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                _ = Server;
                return;
            }
            catch (Exception ex) when (attempt < 3)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        throw new InvalidOperationException(
            "Failed to initialize E2E test server after 3 attempts.",
            lastException);
    }

    private static async Task WaitForDependencyAsync(
        string dependencyName,
        Func<CancellationToken, Task> checkAsync)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Exception? lastException = null;

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                await checkAsync(timeoutCts.Token);
                return;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                break;
            }
        }

        throw new TimeoutException(
            $"Timed out waiting for {dependencyName} to become ready within 30 seconds.",
            lastException);
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

    /// <summary>
     /// Simple IDateTimeService implementation for E2E tests.
    /// </summary>
    private sealed class TestDateTimeService : IDateTimeService
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
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

    private sealed class VerboseTestExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            httpContext.Response.ContentType = "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = exception.GetType().Name,
                Detail = exception.ToString(),
                Instance = httpContext.Request.Path,
            }, cancellationToken: cancellationToken);

            return true;
        }
    }
}
