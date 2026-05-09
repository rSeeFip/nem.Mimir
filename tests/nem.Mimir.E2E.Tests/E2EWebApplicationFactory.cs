using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using nem.Mimir.E2E.Tests.Helpers;
using nem.Mimir.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using nem.Mimir.Application.Common.Interfaces;
using Wolverine;

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
    public IReadOnlyList<string> LogMessages => [];

    public async ValueTask InitializeAsync()
    {
        // 1. Start infrastructure containers
        await _postgres.StartAsync();
        await _rabbitmq.StartAsync();

        // 2. Start WireMock for LiteLLM proxy simulation
        _wireMock = WireMockServer.Start();
        ConfigureDefaultWireMockStubs();

        // 3. Apply EF Core migrations to the real database
        await ApplyMigrationsAsync();

        // 4. Force host startup during fixture initialization so Wolverine
        // discovery/codegen happens once against the finalized test host.
        _ = Server;

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
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Keep this explicit for the B-variant experiment. Removing this entirely
            // causes host startup disposal/initialization failures in the E2E factory.
            services.DisableAllExternalWolverineTransports();
        });

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
                ["LiteLlm:TimeoutSeconds"] = "120",

                // JWT test settings — use symmetric key for test token validation
                ["Jwt:Authority"] = JwtTokenHelper.TestIssuer,
                ["Jwt:Audience"] = JwtTokenHelper.TestAudience,
                ["Jwt:RequireHttpsMetadata"] = "false",
            };

            config.AddInMemoryCollection(testSettings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = E2ETestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = E2ETestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, E2ETestAuthHandler>(E2ETestAuthHandler.SchemeName, _ => { });

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
        var client = base.CreateClient();
        var token = JwtTokenHelper.GenerateToken(userId, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public new HttpClient CreateClient() => base.CreateClient();

    private void ConfigureDefaultWireMockStubs()
    {
        // POST /v1/chat/completions — streaming SSE response
        _wireMock
            .Given(Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/event-stream")
                .WithHeader("Cache-Control", "no-cache")
                .WithHeader("Connection", "keep-alive")
                .WithBody(BuildDefaultSseResponse()));

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

    private static string BuildDefaultSseResponse()
    {
        var sb = new StringBuilder();

        // Chunk 1: role + first content
        sb.AppendLine("data: {\"id\":\"chatcmpl-test123\",\"object\":\"chat.completion.chunk\",\"created\":1234567890,\"model\":\"qwen-2.5-72b\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Hello\"},\"finish_reason\":null}]}");
        sb.AppendLine();

        // Chunk 2: content only
        sb.AppendLine("data: {\"id\":\"chatcmpl-test123\",\"object\":\"chat.completion.chunk\",\"created\":1234567890,\"model\":\"qwen-2.5-72b\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\" from\"},\"finish_reason\":null}]}");
        sb.AppendLine();

        // Chunk 3: content only
        sb.AppendLine("data: {\"id\":\"chatcmpl-test123\",\"object\":\"chat.completion.chunk\",\"created\":1234567890,\"model\":\"qwen-2.5-72b\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\" Mimir\"},\"finish_reason\":null}]}");
        sb.AppendLine();

        // Chunk 4: finish_reason = stop
        sb.AppendLine("data: {\"id\":\"chatcmpl-test123\",\"object\":\"chat.completion.chunk\",\"created\":1234567890,\"model\":\"qwen-2.5-72b\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}");
        sb.AppendLine();

        // [DONE] marker
        sb.AppendLine("data: [DONE]");
        sb.AppendLine();

        return sb.ToString();
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

    /// <summary>
    /// Simple IDateTimeService implementation for E2E tests.
    /// </summary>
    private sealed class TestDateTimeService : IDateTimeService
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class E2ETestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        internal const string SchemeName = "E2ETest";

        private static readonly TokenValidationParameters ValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = JwtTokenHelper.TestIssuer,
            ValidateAudience = true,
            ValidAudience = JwtTokenHelper.TestAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtTokenHelper.TestSigningKey)),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero,
        };

        public E2ETestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var headerValue = authorizationHeader.ToString();
            if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var token = headerValue["Bearer ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing bearer token."));
            }

            try
            {
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, ValidationParameters, out _);
                var ticket = new AuthenticationTicket(principal, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
            {
                return Task.FromResult(AuthenticateResult.Fail(ex));
            }
        }
    }

}
