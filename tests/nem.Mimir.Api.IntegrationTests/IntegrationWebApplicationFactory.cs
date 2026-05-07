using System.IdentityModel.Tokens.Jwt;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using JasperFx;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using nem.Contracts.Identity;
using nem.Contracts.Organism;
using nem.Mimir.Domain.MultiTenancy;
using nem.Mimir.Infrastructure.MultiTenancy;
using nem.Mimir.Infrastructure.Persistence;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Wolverine;

namespace nem.Mimir.Api.IntegrationTests;

/// <summary>
/// Integration-test application factory backed by real PostgreSQL and RabbitMQ
/// Testcontainers, with WireMock.Net standing in for the LiteLLM proxy.
/// </summary>
public sealed class MimirWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultTenantId = "test-tenant";
    private const string DefaultTenantName = "Test Tenant";
    private const string TestIssuer = "mimir-integration-tests";
    private const string TestAudience = "mimir-api";
    private const string TestSigningKey = "super-secret-signing-key-for-integration-tests-12345";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private readonly SemaphoreSlim _resetLock = new(1, 1);

    private WireMockServer _wireMock = null!;
    private Respawner _respawner = null!;

    public WireMockServer WireMock => _wireMock;

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public string RabbitMqConnectionString => _rabbitMq.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbitMq.StartAsync();

        await WaitForPostgresAsync();
        await WaitForRabbitMqAsync();

        _wireMock = WireMockServer.Start();
        ConfigureDefaultWireMockStubs();

        await EnsureServerInitializedAsync();
        await ApplyDatabaseSetupAsync();
        await InitializeRespawnerAsync();
        await ResetStateAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        _wireMock?.Stop();
        _wireMock?.Dispose();

        _resetLock.Dispose();

        await _rabbitMq.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task ResetStateAsync()
    {
        await _resetLock.WaitAsync();

        try
        {
            if (_respawner is null)
            {
                return;
            }

            _wireMock.Reset();
            ConfigureDefaultWireMockStubs();

            await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }
        finally
        {
            _resetLock.Release();
        }
    }

    public new HttpClient CreateClient()
    {
        ResetStateAsync().GetAwaiter().GetResult();
        return base.CreateClient();
    }

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        ResetStateAsync().GetAwaiter().GetResult();
        return base.CreateClient(options);
    }

    public HttpClient CreateAuthenticatedClient(
        string? userId = null,
        string? email = null,
        string? tenantId = null,
        IEnumerable<string>? roles = null)
    {
        var client = CreateClient();
        var token = GenerateToken(userId, email, roles, tenantId ?? DefaultTenantId);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var testSettings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Database:ConnectionString"] = _postgres.GetConnectionString(),
                ["RabbitMQ:ConnectionString"] = _rabbitMq.GetConnectionString(),
                ["LiteLlm:BaseUrl"] = _wireMock.Url!,
                ["LiteLlm:ApiKey"] = "test-api-key",
                ["LiteLlm:TimeoutSeconds"] = "30",
                ["MimirApiOptions:StandaloneMode"] = "false",
                ["Jwt:Authority"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:RequireHttpsMetadata"] = "false",
                ["OpenBao:Enabled"] = "false",
                ["OpenBao:Address"] = "http://127.0.0.1:8200",
                ["OpenBao:Token"] = "test-token",
            };

            config.AddInMemoryCollection(testSettings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.DisableAllExternalWolverineTransports();

            RemoveFactoryHostedServices(services);
            RemoveServiceByTypeName(services, "nem.Mimir.Infrastructure.Health.MimirHealthReportEmitter");
            RemoveServiceByTypeName(services, "nem.Mimir.Infrastructure.McpServers.McpClientStartupService");
            RemoveServiceByTypeName(services, "nem.Mimir.Infrastructure.McpServers.McpConfigChangeListener");

            services.RemoveAll<IDocumentStore>();
            services.RemoveAll<IDocumentSession>();
            services.RemoveAll<IQuerySession>();
            services.AddMarten(options =>
            {
                options.Connection(_postgres.GetConnectionString());
                options.Schema.For<nem.Mimir.Infrastructure.Billing.PersistedCostEvent>()
                    .MultiTenanted()
                    .UniqueIndex(x => x.IdempotencyKey);
                options.AutoCreateSchemaObjects = AutoCreate.All;
            });

            services.AddScoped<IQuerySession>(sp =>
            {
                var store = sp.GetRequiredService<IDocumentStore>();
                var tenantId = sp.GetRequiredService<ITenantContext>().TenantId;
                return string.IsNullOrWhiteSpace(tenantId)
                    ? store.QuerySession()
                    : store.QuerySession(tenantId);
            });

            services.AddScoped<IDocumentSession>(sp =>
            {
                var store = sp.GetRequiredService<IDocumentStore>();
                var tenantId = sp.GetRequiredService<ITenantContext>().TenantId;
                return string.IsNullOrWhiteSpace(tenantId)
                    ? store.LightweightSession()
                    : store.LightweightSession(tenantId);
            });

            services.RemoveAll<TenantContext>();
            services.RemoveAll<ITenantContext>();
            services.AddScoped(_ =>
            {
                var tenantContext = new TenantContext();
                tenantContext.SetTenant(DefaultTenantId, DefaultTenantName);
                return tenantContext;
            });
            services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

            #pragma warning disable EXTEXP0001
            services.AddHttpClient("LiteLlm", client =>
            {
                client.BaseAddress = new Uri(_wireMock.Url!);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Remove("Accept");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Remove("Authorization");
                client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");
            })
            .RemoveAllResilienceHandlers();
            #pragma warning restore EXTEXP0001

            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
            });

            services.AddHealthChecks()
                .AddNpgSql(_postgres.GetConnectionString(), name: "database")
                .AddUrlGroup(new Uri(_wireMock.Url + "/health"), name: "litellm", timeout: TimeSpan.FromSeconds(5));

            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));

                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = TestIssuer,
                        ValidateAudience = true,
                        ValidAudience = TestAudience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = key,
                        NameClaimType = ClaimTypes.NameIdentifier,
                        RoleClaimType = ClaimTypes.Role,
                    };
                });

            services.RemoveAll<IExceptionHandler>();
            services.AddExceptionHandler<VerboseTestExceptionHandler>();
            services.AddSingleton<IHomeostasisAgent, TestHomeostasisAgent>();
            services.AddSingleton<IAutonomyGate, TestAutonomyGate>();
        });
    }

    private async Task ApplyDatabaseSetupAsync()
    {
        await using var scope = Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<MimirDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var documentStore = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await documentStore.Storage.ApplyAllConfiguredChangesToDatabaseAsync(AutoCreate.All);
    }

    private async Task InitializeRespawnerAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__EFMigrationsHistory")],
        });
    }

    private void ConfigureDefaultWireMockStubs()
    {
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
                            }
                        ]
                    }
                    """));

        _wireMock
            .Given(Request.Create()
                .WithPath("/health")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"status\":\"healthy\"}"));
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
                await tcpClient.ConnectAsync(_rabbitMq.Hostname, _rabbitMq.GetMappedPublicPort(5672), cancellationToken);
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
            "Failed to initialize integration test server after 3 attempts.",
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

    private static string GenerateToken(
        string? userId,
        string? email,
        IEnumerable<string>? roles,
        string tenantId)
    {
        var now = DateTime.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId ?? Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, email ?? "integration-tests@example.com"),
            new("tenant_id", tenantId),
            new("tenant_name", DefaultTenantName),
        };

        foreach (var role in roles ?? ["user"])
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            notBefore: now,
            expires: now.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
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
