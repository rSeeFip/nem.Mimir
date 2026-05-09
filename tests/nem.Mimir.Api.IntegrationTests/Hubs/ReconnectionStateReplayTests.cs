using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using nem.Mimir.Api.Hubs;
using nem.Mimir.Api.Services;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Common.Sanitization;
using nem.Mimir.Application.Conversations.Services;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Infrastructure.LiteLlm;
using Shouldly;

namespace nem.Mimir.Api.IntegrationTests.Hubs;

public sealed class ReconnectionStateReplayTests : IAsyncLifetime
{
    private readonly MutableTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);
    private WebApplication? _app;
    private LlmRequestQueue? _requestQueue;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddSignalR();
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, ReplayTestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddSingleton<TimeProvider>(_timeProvider);
        builder.Services.AddSingleton<IReplayBuffer, ReplayBuffer>();

        builder.Services.AddSingleton<IConversationRepository, StubConversationRepository>();
        builder.Services.AddSingleton<IUnitOfWork, StubUnitOfWork>();
        builder.Services.AddSingleton<ILlmService, StubLlmService>();
        builder.Services.AddSingleton<IContextWindowService, StubContextWindowService>();
        builder.Services.AddSingleton<IConversationContextService, StubConversationContextService>();

        _requestQueue = new LlmRequestQueue(NullLogger<LlmRequestQueue>.Instance);
        builder.Services.AddSingleton(_requestQueue);
        builder.Services.AddSingleton<ISanitizationService, StubSanitizationService>();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapHub<ChatHub>("/hubs/chat");

        await _app.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _requestQueue?.Dispose();

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Reconnect_ReplaysBufferedMessages()
    {
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedReplayAsync(conversationId, userId,
        [
            new ReplayMessage(MessageRole.User, "hello", _timeProvider.GetUtcNow()),
            new ReplayMessage(MessageRole.Assistant, "world", _timeProvider.GetUtcNow())
        ]);

        var replayed = await ConnectAndCaptureReplayAsync(conversationId, userId);

        replayed.Count.ShouldBe(2);
        replayed[0].Content.ShouldBe("hello");
        replayed[1].Content.ShouldBe("world");
    }

    [Fact]
    public async Task Reconnect_ReplaysOnlyLast50Messages()
    {
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        for (var index = 1; index <= 60; index++)
        {
            await SeedReplayAsync(conversationId, userId,
            [
                new ReplayMessage(MessageRole.User, $"message-{index}", _timeProvider.GetUtcNow())
            ]);
        }

        var replayed = await ConnectAndCaptureReplayAsync(conversationId, userId);

        replayed.Count.ShouldBe(50);
        replayed[0].Content.ShouldBe("message-11");
        replayed[^1].Content.ShouldBe("message-60");
    }

    [Fact]
    public async Task Reconnect_ReplaysOnlyMessagesWithinFiveMinutes()
    {
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedReplayAsync(conversationId, userId,
        [
            new ReplayMessage(MessageRole.User, "expired", _timeProvider.GetUtcNow())
        ]);

        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        await SeedReplayAsync(conversationId, userId,
        [
            new ReplayMessage(MessageRole.Assistant, "recent", _timeProvider.GetUtcNow())
        ]);

        var replayed = await ConnectAndCaptureReplayAsync(conversationId, userId);

        replayed.Count.ShouldBe(1);
        replayed[0].Content.ShouldBe("recent");
    }

    [Fact]
    public async Task FreshConnect_WithNoBufferedMessages_GetsNoReplay()
    {
        var replayed = await ConnectAndCaptureReplayAsync(Guid.NewGuid(), Guid.NewGuid());

        replayed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reconnect_DoesNotReplayOtherUsersMessagesForSameConversation()
    {
        var conversationId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await SeedReplayAsync(conversationId, userA,
        [
            new ReplayMessage(MessageRole.User, "user-a-message", _timeProvider.GetUtcNow())
        ]);

        await SeedReplayAsync(conversationId, userB,
        [
            new ReplayMessage(MessageRole.Assistant, "user-b-message", _timeProvider.GetUtcNow())
        ]);

        var replayedForA = await ConnectAndCaptureReplayAsync(conversationId, userA);
        var replayedForB = await ConnectAndCaptureReplayAsync(conversationId, userB);

        replayedForA.Count.ShouldBe(1);
        replayedForA[0].Content.ShouldBe("user-a-message");

        replayedForB.Count.ShouldBe(1);
        replayedForB[0].Content.ShouldBe("user-b-message");
    }

    [Fact]
    public async Task Reconnect_DoesNotReplayMessagesFromDifferentConversation()
    {
        var userId = Guid.NewGuid();
        var conversationA = Guid.NewGuid();
        var conversationB = Guid.NewGuid();

        await SeedReplayAsync(conversationA, userId,
        [
            new ReplayMessage(MessageRole.User, "conv-a-message", _timeProvider.GetUtcNow())
        ]);

        await SeedReplayAsync(conversationB, userId,
        [
            new ReplayMessage(MessageRole.Assistant, "conv-b-message", _timeProvider.GetUtcNow())
        ]);

        var replayedForA = await ConnectAndCaptureReplayAsync(conversationA, userId);
        var replayedForB = await ConnectAndCaptureReplayAsync(conversationB, userId);

        replayedForA.Count.ShouldBe(1);
        replayedForA[0].Content.ShouldBe("conv-a-message");

        replayedForB.Count.ShouldBe(1);
        replayedForB[0].Content.ShouldBe("conv-b-message");
    }

    [Fact]
    public async Task Reconnect_AfterExpiryWindow_GetsNoReplay()
    {
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedReplayAsync(conversationId, userId,
        [
            new ReplayMessage(MessageRole.User, "stale-message", _timeProvider.GetUtcNow())
        ]);

        _timeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        var replayed = await ConnectAndCaptureReplayAsync(conversationId, userId);

        replayed.ShouldBeEmpty();
    }

    private async Task SeedReplayAsync(Guid conversationId, Guid userId, IReadOnlyList<ReplayMessage> messages)
    {
        var replayBuffer = _app!.Services.GetRequiredService<IReplayBuffer>();
        foreach (var message in messages)
        {
            await replayBuffer.RecordMessageAsync(conversationId, userId, message);
        }
    }

    private async Task<IReadOnlyList<ReplayMessage>> ConnectAndCaptureReplayAsync(Guid conversationId, Guid userId)
    {
        await using var scope = _app!.Services.CreateAsyncScope();
        var httpContext = CreateHttpContext(conversationId, userId, scope.ServiceProvider);
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = httpContext;

        var callerProxy = new RecordingClientProxy();
        var hub = ActivatorUtilities.CreateInstance<ChatHub>(scope.ServiceProvider);
        hub.Context = new TestHubCallerContext(httpContext);
        hub.Clients = new TestHubCallerClients(callerProxy);
        hub.Groups = new RecordingGroupManager();

        await hub.OnConnectedAsync();

        return callerProxy.ReplayedMessages;
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

    private sealed class MutableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}

internal sealed class TestHubCallerContext : HubCallerContext
{
    private readonly CancellationTokenSource _aborted = new();
    private readonly IDictionary<object, object?> _items = new Dictionary<object, object?>();
    private readonly IFeatureCollection _features = new FeatureCollection();
    private readonly HttpContext _httpContext;

    public TestHubCallerContext(HttpContext httpContext)
    {
        _httpContext = httpContext;
        _features.Set<IHttpContextFeature>(new TestHttpContextFeature(httpContext));
    }

    public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");

    public override string? UserIdentifier => _httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public override ClaimsPrincipal? User => _httpContext.User;

    public override IDictionary<object, object?> Items => _items;

    public override IFeatureCollection Features => _features;

    public override CancellationToken ConnectionAborted => _aborted.Token;

    public override void Abort() => _aborted.Cancel();
}

internal sealed class TestHttpContextFeature(HttpContext httpContext) : IHttpContextFeature
{
    public HttpContext? HttpContext { get; set; } = httpContext;
}

internal sealed class RecordingClientProxy : IClientProxy
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

internal sealed class TestHubCallerClients(RecordingClientProxy callerProxy) : IHubCallerClients
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

internal sealed class RecordingGroupManager : IGroupManager
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

internal sealed class StubConversationRepository : IConversationRepository
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
        => Task.FromResult<nem.Mimir.Domain.Entities.Conversation?>(null);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<PaginatedList<nem.Mimir.Domain.Entities.Message>> GetMessagesAsync(Guid conversationId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class StubUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class StubLlmService : ILlmService
{
    public Task<LlmResponse> SendMessageAsync(string model, IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default)
        => Task.FromResult(new LlmResponse(string.Empty, model, 0, 0, 0, "stop"));

    public async IAsyncEnumerable<LlmStreamChunk> StreamMessageAsync(string model, IReadOnlyList<LlmMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<IReadOnlyList<LlmModelInfoDto>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LlmModelInfoDto>>([]);
}

internal sealed class StubContextWindowService : IContextWindowService
{
    public Task<IReadOnlyList<LlmMessage>> BuildLlmMessagesAsync(nem.Mimir.Domain.Entities.Conversation conversation, string newUserContent, string? model, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LlmMessage>>([]);

    public int GetTokenLimit(string? model) => 0;

    public int EstimateTokenCount(string text) => 0;
}

internal sealed class StubConversationContextService : IConversationContextService
{
    public Task<IReadOnlyList<KnowledgeSearchResultDto>> GetRagContextAsync(Guid conversationId, string query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KnowledgeSearchResultDto>>([]);
}

internal sealed class StubSanitizationService : ISanitizationService
{
    public string SanitizeUserInput(string input) => input;

    public string SanitizeLlmOutput(string output) => output;

    public bool ContainsSuspiciousPatterns(string input) => false;
}

internal sealed class ReplayTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
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
