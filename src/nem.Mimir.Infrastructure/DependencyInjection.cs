namespace nem.Mimir.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Infrastructure.LiteLlm;
using nem.Mimir.Infrastructure.Persistence;
using nem.Mimir.Infrastructure.Persistence.Interceptors;
using nem.Mimir.Infrastructure.Persistence.Repositories;
using nem.Mimir.Infrastructure.Identity;
using nem.Mimir.Infrastructure.Services;
using nem.Mimir.Application.Common.Sanitization;
using Polly;
using Docker.DotNet;
using nem.Mimir.Infrastructure.Plugins;
using nem.Mimir.Infrastructure.Plugins.BuiltIn;
using nem.Mimir.Infrastructure.Agents;
using nem.Mimir.Infrastructure.Tasks;
using nem.Mimir.Infrastructure.Knowledge;
using nem.Mimir.Infrastructure.Cache;
using nem.Contracts.AspNetCore.Classification;

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
        services.AddScoped<IChannelEventRepository, ChannelEventRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ISystemPromptRepository, SystemPromptRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEntityRestoreRepository, EntityRestoreRepository>();

        // Actor identity services
        services.AddScoped<nem.Contracts.Identity.IActorIdentityResolver, EfCoreActorIdentityResolver>();
        services.AddScoped<nem.Contracts.Identity.IActorIdentityService, EfCoreActorIdentityService>();

        // Context window service (scoped — depends on scoped ISystemPromptRepository)
        services.AddScoped<IContextWindowService, ContextWindowService>();

        // Audit service
        services.AddScoped<IAuditService, AuditService>();

        // Sanitization service (singleton — stateless)
        services.Configure<SanitizationSettings>(configuration.GetSection(SanitizationSettings.SectionName));
        services.AddSingleton<ISanitizationService, SanitizationService>();
        // LiteLLM options
        services.Configure<LiteLlmOptions>(configuration.GetSection(LiteLlmOptions.SectionName));
        services.Configure<ClassificationOptions>(configuration.GetSection(ClassificationOptions.SectionName));

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

        var classificationSection = configuration.GetSection(ClassificationOptions.SectionName);
        var classificationBaseUrl = classificationSection.GetValue<string>(nameof(ClassificationOptions.ClassificationApiBaseUrl)) ?? "http://localhost:5100";

        services.AddHttpClient("ClassificationApi", client =>
        {
            client.BaseAddress = new Uri(classificationBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Request queue (singleton — one queue for the whole app)
        services.AddSingleton<LlmRequestQueue>();

        // LLM service
        services.AddScoped<ILlmService, LiteLlmClient>();

        // Docker sandbox service
        // DockerClient is thread-safe (uses HttpClient internally) — Singleton is the correct lifetime.
        // See: https://github.com/dotnet/Docker.DotNet
#pragma warning disable CS0618
        services.AddSingleton<IDockerClient>(_ => new DockerClientConfiguration().CreateClient());
        services.AddScoped<ISandboxService, SandboxService>();
#pragma warning restore CS0618

        // Plugin service (singleton — manages plugin lifecycle)
        services.AddSingleton<IPluginService, PluginManager>();

        // Built-in plugins
        services.AddSingleton<CodeRunnerPlugin>();
        services.AddSingleton<WebSearchPlugin>();
        services.AddHostedService<BuiltInPluginRegistrar>();

        // System prompt service (singleton — stateless template rendering)
        services.AddSingleton<ISystemPromptService, SystemPromptService>();

        // Conversation archive service
        services.AddScoped<IConversationArchiveService, ConversationArchiveService>();

        services.AddAgentCommunicationBus();
        services.AddBackgroundTaskInfrastructure(configuration);
        services.AddKnowHubIntegration(configuration);

        services.AddSingleton<SemanticCacheOptions>();
        services.AddSingleton<global::nem.Contracts.TokenOptimization.ISemanticCache, SemanticCacheService>();

        return services;
    }
}
