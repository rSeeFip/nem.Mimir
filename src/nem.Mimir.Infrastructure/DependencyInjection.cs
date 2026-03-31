namespace nem.Mimir.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.McpServers;
using nem.Mimir.Infrastructure.LiteLlm;
using nem.Mimir.Infrastructure.McpServers;
using nem.Mimir.Infrastructure.Persistence;
using nem.Mimir.Infrastructure.Persistence.Interceptors;
using nem.Mimir.Infrastructure.Persistence.Repositories;
using nem.Mimir.Infrastructure.Services;
using nem.Mimir.Application.Common.Sanitization;
using Polly;
using Docker.DotNet;
using nem.Mimir.Infrastructure.Plugins;
using nem.Mimir.Infrastructure.Plugins.BuiltIn;
using nem.Mimir.Infrastructure.Tools;
using nem.Mimir.Domain.Tools;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();

        services.AddDbContext<MimirDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(MimirDbContext).Assembly.FullName);
                });
        });

        // Repositories and Unit of Work
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ISystemPromptRepository, SystemPromptRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEntityRestoreRepository, EntityRestoreRepository>();
        services.AddScoped<IMcpServerConfigRepository, McpServerConfigRepository>();
        services.AddScoped<IToolWhitelistService, ToolWhitelistService>();

        // Context window service (scoped — depends on scoped ISystemPromptRepository)
        services.AddScoped<IContextWindowService, ContextWindowService>();

 // Audit service
 services.AddScoped<IAuditService, AuditService>();

        // Sanitization service (singleton — stateless)
        services.Configure<SanitizationSettings>(configuration.GetSection(SanitizationSettings.SectionName));
        services.AddSingleton<ISanitizationService, SanitizationService>();
        // LiteLLM options
        services.Configure<LiteLlmOptions>(configuration.GetSection(LiteLlmOptions.SectionName));

        // LiteLLM HTTP client with Polly resilience
        var liteLlmSection = configuration.GetSection(LiteLlmOptions.SectionName);
        var baseUrl = liteLlmSection.GetValue<string>("BaseUrl") ?? "http://localhost:4000";
        var timeoutSeconds = liteLlmSection.GetValue<int?>("TimeoutSeconds") ?? 120;
        var apiKey = liteLlmSection.GetValue<string>("ApiKey") ?? string.Empty;

        services.AddHttpClient(LiteLlmClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        })
        .AddResilienceHandler("LiteLlmResilience", builder =>
        {
            // Retry: 3 attempts, exponential backoff (1s, 2s, 4s)
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = static args => ValueTask.FromResult(
                    args.Outcome.Result?.StatusCode is
                        System.Net.HttpStatusCode.TooManyRequests or
                        System.Net.HttpStatusCode.InternalServerError or
                        System.Net.HttpStatusCode.BadGateway or
                        System.Net.HttpStatusCode.ServiceUnavailable
                    || args.Outcome.Exception is HttpRequestException),
            });

            // Circuit breaker: 5 failures in 30s → break for 30s
            builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
            });
        });

        // Request queue (singleton — one queue for the whole app)
        services.AddSingleton<LlmRequestQueue>();

        // LLM service
        services.AddScoped<ILlmService, LiteLlmClient>();

        // Docker sandbox service
        // DockerClient is thread-safe (uses HttpClient internally) — Singleton is the correct lifetime.
        // See: https://github.com/dotnet/Docker.DotNet
        services.AddSingleton<IDockerClient>(_ => new DockerClientConfiguration().CreateClient());
        services.AddScoped<ISandboxService, SandboxService>();

        // Plugin service (singleton — manages plugin lifecycle)
        services.AddSingleton<IPluginService, PluginManager>();

        // Built-in plugins
        services.AddSingleton<CodeRunnerPlugin>();
        services.AddHostedService<BuiltInPluginRegistrar>();

        services.AddSingleton<Health.MimirHealthReportEmitter>();
        services.AddHostedService(sp => sp.GetRequiredService<Health.MimirHealthReportEmitter>());

        services.AddSingleton<IToolAuditLogger, ToolAuditLogger>();

        // Tool providers → CompositeToolProvider → AuditingDecorator → IToolProvider
        services.AddSingleton<PluginToolProvider>();
        services.AddSingleton<McpToolProvider>();
        services.AddSingleton<IToolProvider>(sp =>
        {
            var providers = new IToolProvider[]
            {
                sp.GetRequiredService<PluginToolProvider>(),
                sp.GetRequiredService<McpToolProvider>(),
            };
            var composite = new CompositeToolProvider(providers);
            var auditLogger = sp.GetRequiredService<IToolAuditLogger>();
            return new AuditingToolProviderDecorator(composite, auditLogger);
        });

        // System prompt service (singleton — stateless template rendering)
        services.AddSingleton<ISystemPromptService, SystemPromptService>();

        // Conversation archive service
        services.AddScoped<IConversationArchiveService, ConversationArchiveService>();

        // MCP client manager (singleton — long-lived connections)
        services.AddSingleton<IMcpClientManager, McpClientManager>();
        services.AddHostedService<McpClientStartupService>();

        // MCP config change listener (singleton — polls for runtime config changes)
        services.AddSingleton<McpConfigChangeListener>();
        services.AddHostedService(sp => sp.GetRequiredService<McpConfigChangeListener>());

        return services;
    }
}
