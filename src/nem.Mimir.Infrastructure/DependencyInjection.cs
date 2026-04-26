namespace nem.Mimir.Infrastructure;

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Marten;
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
using nem.Mimir.Infrastructure.Mcp;
using nem.Mimir.Finance.McpTools;
using nem.Mimir.Application.Knowledge;
using nem.Mimir.Application.Conversations.Services;
using nem.Contracts.AspNetCore.Classification;
using nem.Contracts.Classification;
using nem.Contracts.Lifecycle;
using nem.Contracts.Inference;
using nem.Mimir.Application.Analysis;
using nem.Mimir.Infrastructure.Analysis;
using nem.Mimir.Infrastructure.Lifecycle;
using nem.Mimir.Application.Agents.Selection;
using nem.Mimir.Application.Agents.Services;
using nem.Mimir.Infrastructure.Adapters;
using nem.Mimir.Infrastructure.Organism.MapeK;
using nem.Mimir.Infrastructure.Inference;
using nem.Mimir.Domain.Plugins;
using nem.Mimir.Infrastructure.Workflow;
using nem.Mimir.Application.Notes.Services;

public static class DependencyInjection
{
    [SuppressMessage("Compiler", "CS0618", Justification = "Legacy Yjs storage remains registered for backward-compatible note migration paths.")]
    [SuppressMessage("Compiler", "CS0618", Justification = "Docker.DotNet sandbox remains registered until all callers complete the OpenSandbox migration.")]
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

        var defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetSection("Database:ConnectionString").Value
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required for Automerge document storage.");

        services.AddMarten(options =>
        {
            options.Connection(defaultConnectionString);
            options.DatabaseSchemaName = "mimir_collaboration";

            var automergeState = options.Schema.For<AutomergeDocumentStoreDocument>();
            automergeState.Identity(x => x.Id);
            automergeState.DocumentAlias("automerge_document_store");
            automergeState.Duplicate(x => x.DocumentId);
            automergeState.Index(x => x.DocumentId);
            automergeState.Index(x => x.UpdatedAt);
        });

        // Repositories and Unit of Work
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IConversationSearchService, ConversationSearchService>();
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<IEvaluationRepository, EvaluationRepository>();
        services.AddScoped<IImageGenerationRepository, ImageGenerationRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<IChannelEventRepository, ChannelEventRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ISystemPromptRepository, SystemPromptRepository>();
        services.AddScoped<IPromptTemplateRepository, PromptTemplateRepository>();
        services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
        services.AddScoped<IModelProfileRepository, ModelProfileRepository>();
        services.AddScoped<IArenaConfigRepository, ArenaConfigRepository>();
        services.AddScoped<IUserMemoryRepository, UserMemoryRepository>();
        services.AddScoped<IKnowledgeCollectionRepository, KnowledgeCollectionRepository>();
        services.AddScoped<IUsageStatsReadDbContext>(sp => sp.GetRequiredService<MimirDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEntityRestoreRepository, EntityRestoreRepository>();
        services.AddScoped<AutomergeDocumentStore>();
        services.AddScoped<IImageGenerationService, ImageGenerationService>();

        // Actor identity services
        services.AddScoped<nem.Contracts.Identity.IActorIdentityResolver, EfCoreActorIdentityResolver>();
        services.AddScoped<nem.Contracts.Identity.IActorIdentityService, EfCoreActorIdentityService>();

        // Context window service (scoped — depends on scoped ISystemPromptRepository)
        services.AddScoped<IContextWindowService, ContextWindowService>();

        // Audit service
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITrajectoryRecorder, TrajectoryRecorder>();
        services.AddScoped<ITrajectoryAnalyzer, LlmTrajectoryAnalyzer>();
        services.AddScoped<ISkillQualityProvider, NoOpSkillQualityProvider>();

        // Sanitization service (singleton — stateless)
        services.Configure<SanitizationSettings>(configuration.GetSection(SanitizationSettings.SectionName));
        services.AddSingleton<ISanitizationService, SanitizationService>();
        // LiteLLM options
        services.Configure<LiteLlmOptions>(configuration.GetSection(LiteLlmOptions.SectionName));
        services.Configure<ClassificationOptions>(configuration.GetSection(ClassificationOptions.SectionName));
        services.Configure<SearxngOptions>(configuration.GetSection(SearxngOptions.SectionName));
        services.Configure<MediaHubOptions>(configuration.GetSection(MediaHubOptions.SectionName));
        services.Configure<SkillsMarketplaceOptions>(configuration.GetSection(SkillsMarketplaceOptions.SectionName));
        services.AddScoped<IClassificationContext, ClassificationContext>();

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

        services.AddHttpClient(LiteLlmHealthCheck.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        })
        .AddResilienceHandler("LiteLlmHealthResilience", builder =>
        {
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

        var searxngSection = configuration.GetSection(SearxngOptions.SectionName);
        var searxngBaseUrl = searxngSection.GetValue<string>(nameof(SearxngOptions.BaseUrl)) ?? "http://localhost:8081";
        var searxngTimeoutSeconds = searxngSection.GetValue<int?>(nameof(SearxngOptions.TimeoutSeconds)) ?? 10;

        services.AddHttpClient<ISearxngClient, SearxngClient>(client =>
        {
            client.BaseAddress = new Uri(searxngBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(searxngTimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        var skillsMarketplaceSection = configuration.GetSection(SkillsMarketplaceOptions.SectionName);
        var skillsMarketplaceBaseUrl = skillsMarketplaceSection.GetValue<string>(nameof(SkillsMarketplaceOptions.BaseUrl));
        var skillsMarketplaceTimeoutSeconds = skillsMarketplaceSection.GetValue<int?>(nameof(SkillsMarketplaceOptions.TimeoutSeconds)) ?? 30;

        services.AddHttpClient(SkillMarketplacePlugin.SkillsClientName, client =>
        {
            if (!string.IsNullOrWhiteSpace(skillsMarketplaceBaseUrl))
            {
                client.BaseAddress = new Uri(skillsMarketplaceBaseUrl, UriKind.Absolute);
            }

            client.Timeout = TimeSpan.FromSeconds(skillsMarketplaceTimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddSingleton<IMediaHubClient, MediaHubClient>();
        services.AddSingleton<IKnowledgeIngestionService, KnowHubIngestionService>();

        // Request queue (singleton — one queue for the whole app)
        services.AddSingleton<LlmRequestQueue>();

        // LLM service
        services.AddScoped<ILlmService, LiteLlmClient>();
        services.AddScoped<IInferenceGateway, InferenceGatewayService>();
        services.AddScoped<IWorkflowOrchestrationBridge, WorkflowOrchestrationBridge>();

        // Docker sandbox service
        // DockerClient is thread-safe (uses HttpClient internally) — Singleton is the correct lifetime.
        services.AddSingleton<IDockerClient>(_ => new DockerClientConfiguration().CreateClient());

        // Plugin service (singleton — manages plugin lifecycle)
        services.AddSingleton<PluginManager>();
        services.AddSingleton<IPluginService>(sp => sp.GetRequiredService<PluginManager>());
        services.AddSingleton<IPluginRuntimeCatalog>(sp => sp.GetRequiredService<PluginManager>());

        // Built-in plugins
        services.AddSingleton<CodeRunnerPlugin>();
        services.AddSingleton<WebSearchPlugin>();
        services.AddSingleton<SkillMarketplacePlugin>();
        services.AddSingleton<FinanceToolRegistryPlugin>();
        services.AddSingleton<IBuiltInPlugin>(sp => sp.GetRequiredService<CodeRunnerPlugin>());
        services.AddSingleton<IBuiltInPlugin>(sp => sp.GetRequiredService<WebSearchPlugin>());
        services.AddSingleton<IBuiltInPlugin>(sp => sp.GetRequiredService<SkillMarketplacePlugin>());
        services.AddSingleton<IBuiltInPlugin>(sp => sp.GetRequiredService<FinanceToolRegistryPlugin>());
        services.AddHostedService<BuiltInPluginRegistrar>();

        // System prompt service (singleton — stateless template rendering)
        services.AddSingleton<ISystemPromptService, SystemPromptService>();

        // Conversation archive service
        services.AddScoped<IConversationArchiveService, ConversationArchiveService>();
        services.AddScoped<IConversationKnowledgeProvider, ConversationKnowledgeProvider>();
        services.AddScoped<IConversationContextService, ConversationRagService>();

        services.AddAgentCommunicationBus();
        services.AddGlobalWorkspaceAdapter(configuration);
        services.AddBackgroundTaskInfrastructure(configuration);
        services.AddMcpClient(configuration);
        services.AddKnowHubIntegration(configuration);

        services.AddSingleton<SemanticCacheOptions>();
        services.AddSingleton<global::nem.Contracts.TokenOptimization.ISemanticCache, SemanticCacheService>();

        // Lifecycle services (data subject, erasure, pruning, retention policy cache)
        services.AddScoped<IDataSubjectContributor, MimirDataSubjectContributor>();
        services.AddScoped<IReadModelPruningStrategy, MimirReadModelPruningStrategy>();
        services.AddSingleton<MimirRetentionPolicyCache>();
        services.AddMimirMapeK();

        return services;
    }
}
