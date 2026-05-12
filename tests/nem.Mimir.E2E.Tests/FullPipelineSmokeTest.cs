using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Contracts.Inference;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Api.Hubs;
using nem.Mimir.Api.Services;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Common.Sanitization;
using nem.Mimir.Application.Conversations.Services;
using nem.Mimir.Application.Guardrails;
using nem.Mimir.Application.Guardrails.Bundles;
using nem.Mimir.Application.Llm;
using nem.Mimir.Application.Tokens;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Infrastructure.Guardrails;
using nem.Mimir.Infrastructure.Inference;
using nem.Mimir.Infrastructure.LiteLlm;
using nem.Mimir.Infrastructure.Tokens;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.E2E.Tests;

public sealed class FullPipelineSmokeTest
{
    [Fact]
    public async Task FullPipeline_SingleRequest_FlowsThroughPolicyBudgetGuardrailCascadeAndLlm()
    {
        var stages = new List<string>();
        var messages = new[]
        {
            new LlmMessage("user", "Provide a short deployment status summary."),
        };

        var policy = Substitute.For<IInferencePolicy>();
        policy
            .EvaluateAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stages.Add("policy");
                return Task.FromResult(new PolicyResult(PolicyAction.Redirect, RedirectAlias: InferenceModelAlias.Fast.Value));
            });

        var modelCascade = Substitute.For<IModelCascade>();
        modelCascade
            .SelectModelAsync(Arg.Any<ModelSelectionContext>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stages.Add("model-cascade");
                return Task.FromResult(new ModelConfig(LlmModels.Fast, "LiteLLM", ModelTier.Fast, 0.0001m, 0.0002m, 131072));
            });

        var gateway = new InferenceGatewayService(modelCascade, NullLogger<InferenceGatewayService>.Instance);
        var modelResolution = new GatewayBackedModelResolution(gateway);

        var tracker = Substitute.For<ITokenTracker>();
        tracker
            .GetUsageAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stages.Add("budget-check");
                return Task.FromResult(new TokenUsageSummary("nem.mimir", 120, 30, 0m, 1));
            });
        tracker
            .RecordUsageAsync(Arg.Any<TokenUsageEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stages.Add("budget-record");
                return Task.CompletedTask;
            });

        var governor = new TokenBudgetGovernor(
            tracker,
            Options.Create(new TokenBudgetGovernorOptions
            {
                DefaultBudget = 10_000,
                WarnThresholdPercent = 80,
            }));

        var guardrailEngine = new GuardrailPolicyEngine(Options.Create(new GuardrailsOptions
        {
            Enabled = true,
            MaxOutputTokens = 4096,
        }));
        var evaluator = Substitute.For<IGuardrailEvaluator>();
        evaluator
            .EvaluateAsync(Arg.Any<GuardrailRequest>(), Arg.Any<IGuardrailBundle>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                stages.Add("guardrail");
                return guardrailEngine.EvaluateAsync(
                    ci.Arg<GuardrailRequest>(),
                    ci.Arg<IGuardrailBundle>(),
                    ci.Arg<CancellationToken>());
            });

        var inner = new RecordingLlmService(
            stages,
            new LlmResponse("smoke-ok", LlmModels.Fast, 12, 8, 20, "stop"));

        var overlay = new PolicyIntentOverlayService(gateway, policy, modelResolution);
        ILlmService pipeline = new PolicyLlmServiceDecorator(
            new BudgetLlmServiceDecorator(
                new GuardrailLlmServiceDecorator(inner, evaluator, new StandardBundle()),
                governor),
            overlay);

        var response = await pipeline.SendMessageAsync(LlmModels.Primary, messages, CancellationToken.None);

        response.Content.ShouldBe("smoke-ok");
        response.Model.ShouldBe(LlmModels.Fast);
        inner.LastRequestedModel.ShouldBe(LlmModels.Fast);
        stages.ShouldBe([
            "policy",
            "model-cascade",
            "budget-check",
            "guardrail",
            "llm",
            "budget-record",
        ]);

        await modelCascade.Received(1).SelectModelAsync(
            Arg.Is<ModelSelectionContext>(context =>
                context.TaskType == InferenceModelAlias.Fast.Value &&
                context.RequiresReasoning == false),
            Arg.Any<CancellationToken>());
        await tracker.Received(1).RecordUsageAsync(
            Arg.Is<TokenUsageEvent>(usage =>
                usage.ModelId == LlmModels.Fast &&
                usage.InputTokens == 12 &&
                usage.OutputTokens == 8),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FullPipeline_StreamingReplayScenario_KeepsSignalRReplayAndStreamingMiddlewareActive()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var conversationId = Guid.NewGuid();
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddSignalR();
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, ReplayTestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddSingleton<TimeProvider>(timeProvider);
        builder.Services.Configure<ReplayBufferOptions>(options =>
        {
            options.MaxMessages = 50;
            options.WindowMinutes = 5;
        });
        builder.Services.AddSingleton<IReplayBuffer, ReplayBuffer>();
        builder.Services.AddSingleton<IConversationRepository, StubConversationRepository>();
        builder.Services.AddSingleton<IUnitOfWork, StubUnitOfWork>();
        builder.Services.AddSingleton<IContextWindowService, StubContextWindowService>();
        builder.Services.AddSingleton<IConversationContextService, StubConversationContextService>();
        builder.Services.AddSingleton<ISanitizationService, StubSanitizationService>();
        builder.Services.AddSingleton(new LlmRequestQueue(NullLogger<LlmRequestQueue>.Instance));
        builder.Services.AddSingleton<ILlmService>(new StubStreamingLlmService());

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHub<ChatHub>("/hubs/chat");
        await app.StartAsync();

        var replayBuffer = app.Services.GetRequiredService<IReplayBuffer>();
        await replayBuffer.RecordMessageAsync(
            conversationId,
            userId,
            new ReplayMessage(MessageRole.User, "hello", timeProvider.GetUtcNow()),
            CancellationToken.None);
        await replayBuffer.RecordMessageAsync(
            conversationId,
            userId,
            new ReplayMessage(MessageRole.Assistant, "world", timeProvider.GetUtcNow()),
            CancellationToken.None);

        await using var scope = app.Services.CreateAsyncScope();
        var httpContext = CreateHttpContext(conversationId, userId, scope.ServiceProvider);
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = httpContext;

        var callerProxy = new RecordingClientProxy();
        var hub = ActivatorUtilities.CreateInstance<ChatHub>(scope.ServiceProvider);
        hub.Context = new TestHubCallerContext(httpContext);
        hub.Clients = new TestHubCallerClients(callerProxy);
        hub.Groups = new RecordingGroupManager();

        await hub.OnConnectedAsync();

        callerProxy.ReplayedMessages.Count.ShouldBe(2);
        callerProxy.ReplayedMessages[0].Content.ShouldBe("hello");
        callerProxy.ReplayedMessages[1].Content.ShouldBe("world");

        var streamed = new List<ChatToken>();
        await foreach (var token in hub.SendMessage(conversationId.ToString("D"), "Stream a reply", LlmModels.Fast, CancellationToken.None))
        {
            streamed.Add(token);
        }

        streamed.ShouldNotBeEmpty();
        streamed[^1].IsComplete.ShouldBeTrue();
        string.Concat(streamed.Select(token => token.Token)).ShouldContain("stream-complete");
    }

    [Fact]
    public void FullPipeline_AppSettings_LoadsSmokeConfigGroups()
    {
        var apiProjectPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src/nem.Mimir.Api"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.Configure<InferencePolicyOptions>(configuration.GetSection(InferencePolicyOptions.SectionName));
        services.Configure<TokenBudgetGovernorOptions>(configuration.GetSection(TokenBudgetGovernorOptions.SectionName));
        services.Configure<GuardrailsOptions>(configuration.GetSection(GuardrailsOptions.SectionName));
        services.Configure<ReplayBufferOptions>(configuration.GetSection(ReplayBufferOptions.SectionName));

        using var provider = services.BuildServiceProvider();

        var inferencePolicy = provider.GetRequiredService<IOptions<InferencePolicyOptions>>().Value;
        var budget = provider.GetRequiredService<IOptions<TokenBudgetGovernorOptions>>().Value;
        var guardrails = provider.GetRequiredService<IOptions<GuardrailsOptions>>().Value;
        var replay = provider.GetRequiredService<IOptions<ReplayBufferOptions>>().Value;

        inferencePolicy.Action.ShouldBe(PolicyAction.Allow);
        inferencePolicy.Enabled.ShouldBeTrue();
        inferencePolicy.DefaultPolicy.ShouldBe("allow-all");
        budget.DefaultBudget.ShouldBe(5000);
        budget.WarnThresholdPercent.ShouldBe(80d);
        guardrails.Enabled.ShouldBeTrue();
        guardrails.MaxOutputTokens.ShouldBe(4096);
        guardrails.ActiveBundle.ShouldBe("standard");
        guardrails.AvailableBundles.ShouldBe(["permissive", "standard", "strict"]);
        replay.MaxMessages.ShouldBe(50);
        replay.WindowMinutes.ShouldBe(5);
    }

    private static DefaultHttpContext CreateHttpContext(Guid conversationId, Guid userId, IServiceProvider services)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = services;
        httpContext.Request.QueryString = new QueryString($"?conversationId={conversationId:D}&testUserId={userId:D}");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D"))
        ],
        "Test"));

        return httpContext;
    }

    private sealed class GatewayBackedModelResolution(InferenceGatewayService gateway) : IModelResolution
    {
        public async Task<ResolvedModel> ResolveAsync(
            InferenceModelAlias alias,
            PolicyContext context,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            return await gateway.ResolveModelAsync(alias, ct)
                ?? throw new InvalidOperationException($"Could not resolve alias '{alias}'.");
        }
    }

    private sealed class RecordingLlmService(List<string> stages, LlmResponse response) : ILlmService
    {
        public string? LastRequestedModel { get; private set; }

        public Task<LlmResponse> SendMessageAsync(
            string model,
            IReadOnlyList<LlmMessage> messages,
            CancellationToken cancellationToken = default)
        {
            LastRequestedModel = model;
            stages.Add("llm");
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<LlmStreamChunk> StreamMessageAsync(
            string model,
            IReadOnlyList<LlmMessage> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastRequestedModel = model;
            stages.Add("llm-stream");
            yield return new LlmStreamChunk(response.Content, response.Model, response.FinishReason);
            await Task.CompletedTask;
        }

        public Task<IReadOnlyList<LlmModelInfoDto>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LlmModelInfoDto>>([]);
    }

    private sealed class MutableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    private sealed class StubConversationRepository : IConversationRepository
    {
        public Task<nem.Mimir.Domain.Entities.Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<nem.Mimir.Domain.Entities.Conversation?>(null);

        public Task<PaginatedList<nem.Mimir.Domain.Entities.Conversation>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<nem.Mimir.Domain.Entities.Conversation>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<nem.Mimir.Domain.Entities.Conversation>>([]);

        public Task<nem.Mimir.Domain.Entities.Conversation> CreateAsync(nem.Mimir.Domain.Entities.Conversation conversation, CancellationToken cancellationToken = default)
            => Task.FromResult(conversation);

        public Task UpdateAsync(nem.Mimir.Domain.Entities.Conversation conversation, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<nem.Mimir.Domain.Entities.Conversation?> GetWithMessagesAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<nem.Mimir.Domain.Entities.Conversation?>(CreateConversation(id));

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PaginatedList<nem.Mimir.Domain.Entities.Message>> GetMessagesAsync(Guid conversationId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private static nem.Mimir.Domain.Entities.Conversation CreateConversation(Guid conversationId)
        {
            var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var conversation = nem.Mimir.Domain.Entities.Conversation.Create(userId, "Full Pipeline Smoke");
            typeof(nem.Mimir.Domain.Entities.Conversation)
                .GetProperty(nameof(nem.Mimir.Domain.Entities.Conversation.Id))!
                .SetValue(conversation, conversationId);
            return conversation;
        }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class StubStreamingLlmService : ILlmService
    {
        public Task<LlmResponse> SendMessageAsync(string model, IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new LlmResponse("stream-complete", model, 0, 0, 0, "stop"));

        public async IAsyncEnumerable<LlmStreamChunk> StreamMessageAsync(string model, IReadOnlyList<LlmMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new LlmStreamChunk("stream-", model, null);
            yield return new LlmStreamChunk("complete", model, "stop");
            await Task.CompletedTask;
        }

        public Task<IReadOnlyList<LlmModelInfoDto>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LlmModelInfoDto>>([]);
    }

    private sealed class StubContextWindowService : IContextWindowService
    {
        public Task<IReadOnlyList<LlmMessage>> BuildLlmMessagesAsync(nem.Mimir.Domain.Entities.Conversation conversation, string newUserContent, string? model, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LlmMessage>>([new LlmMessage("user", newUserContent)]);

        public int GetTokenLimit(string? model) => 8192;

        public int EstimateTokenCount(string text) => Math.Max(1, text.Length / 4);
    }

    private sealed class StubConversationContextService : IConversationContextService
    {
        public Task<IReadOnlyList<KnowledgeSearchResultDto>> GetRagContextAsync(Guid conversationId, string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<KnowledgeSearchResultDto>>([]);
    }

    private sealed class StubSanitizationService : ISanitizationService
    {
        public string SanitizeUserInput(string input) => input;

        public string SanitizeLlmOutput(string output) => output;

        public bool ContainsSuspiciousPatterns(string input) => false;
    }

    private sealed class TestHubCallerContext : HubCallerContext
    {
        private readonly CancellationTokenSource _aborted = new();
        private readonly IDictionary<object, object?> _items = new Dictionary<object, object?>();
        private readonly IFeatureCollection _features = new FeatureCollection();

        public TestHubCallerContext(HttpContext httpContext)
        {
            HttpContext = httpContext;
            _features.Set<IHttpContextFeature>(new TestHttpContextFeature(httpContext));
        }

        private HttpContext HttpContext { get; }

        public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");

        public override string? UserIdentifier => HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        public override ClaimsPrincipal? User => HttpContext.User;

        public override IDictionary<object, object?> Items => _items;

        public override IFeatureCollection Features => _features;

        public override CancellationToken ConnectionAborted => _aborted.Token;

        public override void Abort() => _aborted.Cancel();
    }

    private sealed class TestHttpContextFeature(HttpContext httpContext) : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        public IReadOnlyList<ReplayMessage> ReplayedMessages { get; private set; } = [];

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (method == "ReplayMessages" && args.Length > 0 && args[0] is IReadOnlyList<ReplayMessage> replayMessages)
            {
                ReplayedMessages = replayMessages.ToArray();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestHubCallerClients(RecordingClientProxy callerProxy) : IHubCallerClients
    {
        public IClientProxy All => callerProxy;
        public IClientProxy Caller => callerProxy;
        public IClientProxy Others => callerProxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => callerProxy;
        public IClientProxy Client(string connectionId) => callerProxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => callerProxy;
        public IClientProxy Group(string groupName) => callerProxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => callerProxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => callerProxy;
        public IClientProxy OthersInGroup(string groupName) => callerProxy;
        public IClientProxy User(string userId) => callerProxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => callerProxy;
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public ConcurrentBag<(string ConnectionId, string GroupName)> AddedGroups { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ReplayTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public ReplayTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userId = Request.Query["testUserId"].ToString();
            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing or invalid testUserId query parameter."));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, parsedUserId.ToString("D"))
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
