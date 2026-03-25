namespace nem.Mimir.Teams.Tests.Services;

using System.Text.Json;
using Wolverine;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.ChannelEvents;
using nem.Mimir.Teams.Configuration;
using nem.Mimir.Teams.Services;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using NSubstitute;
using Shouldly;

public sealed class TeamsChannelAdapterTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ActivityConverter _activityConverter = new();
    private readonly AdaptiveCardBuilder _cardBuilder = new();
    private readonly AadClaimMapper _claimMapper = new();

    private TeamsChannelAdapter CreateAdapter(TeamsSettings? settings = null)
    {
        var opts = Options.Create(settings ?? new TeamsSettings
        {
            AppId = "test-app-id",
            AppPassword = "test-password",
        });

        return new TeamsChannelAdapter(
            opts,
            _bus,
            _activityConverter,
            _cardBuilder,
            _claimMapper,
            NullLogger<TeamsChannelAdapter>.Instance);
    }

    // --- IChannelEventSource ---

    [Fact]
    public void Channel_ReturnsTeams()
    {
        var adapter = CreateAdapter();

        adapter.Channel.ShouldBe(ChannelType.Teams);
    }

    [Fact]
    public async Task StartAsync_EmptyAppId_ReturnsWithoutStarting()
    {
        var adapter = CreateAdapter(new TeamsSettings { AppId = string.Empty });
        using var cts = new CancellationTokenSource();

        // Should not throw
        await adapter.StartAsync(cts.Token);
        await adapter.StopAsync(CancellationToken.None);
    }

    // --- IChannelMessageSender ---

    [Fact]
    public async Task SendAsync_TextContent_ReturnsSuccess()
    {
        var adapter = CreateAdapter();
        var content = new TextContent("Hello");

        var result = await adapter.SendAsync(
            "conversation-id-123",
            content,
            TestContext.Current.CancellationToken);

        // With no real Bot Framework client, the adapter should still succeed structurally
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
    }

    // --- Webhook processing (IBot.OnTurnAsync) ---

    [Fact]
    public async Task OnTurnAsync_MessageActivity_DispatchesIngestCommand()
    {
        var adapter = CreateAdapter();
        _bus.InvokeAsync<ChannelEventResult>(Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(new ChannelEventResult(Guid.NewGuid(), true));

        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "user message",
            ChannelId = "msteams",
            Conversation = new ConversationAccount { Id = "conv-123" },
            From = new ChannelAccount { Id = "user-456", Name = "Test User" },
            Timestamp = DateTimeOffset.UtcNow,
        };

        var turnContext = CreateTurnContext(activity);

        await adapter.OnTurnAsync(turnContext, TestContext.Current.CancellationToken);

        await _bus.Received(1).InvokeAsync<ChannelEventResult>(
            Arg.Is<IngestChannelEventCommand>(cmd =>
                cmd.Channel == ChannelType.Teams &&
                cmd.ExternalChannelId == "conv-123" &&
                cmd.ExternalUserId == "user-456"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task OnTurnAsync_NonMessageActivity_DoesNotDispatch()
    {
        var adapter = CreateAdapter();
        var activity = new Activity
        {
            Type = ActivityTypes.ConversationUpdate,
            ChannelId = "msteams",
            Conversation = new ConversationAccount { Id = "conv-123" },
            From = new ChannelAccount { Id = "user-456" },
        };

        var turnContext = CreateTurnContext(activity);

        await adapter.OnTurnAsync(turnContext, TestContext.Current.CancellationToken);

        await _bus.DidNotReceive().InvokeAsync<ChannelEventResult>(
            Arg.Any<object>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task OnEventReceived_IsRaisedOnMessage()
    {
        var adapter = CreateAdapter();
        ChannelEvent? receivedEvent = null;
        adapter.OnEventReceived += evt =>
        {
            receivedEvent = evt;
            return Task.CompletedTask;
        };

        _bus.InvokeAsync<ChannelEventResult>(Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(new ChannelEventResult(Guid.NewGuid(), true));

        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "hello",
            ChannelId = "msteams",
            Conversation = new ConversationAccount { Id = "conv-789" },
            From = new ChannelAccount { Id = "user-012", Name = "Raiser" },
            Timestamp = DateTimeOffset.UtcNow,
        };

        var turnContext = CreateTurnContext(activity);
        await adapter.OnTurnAsync(turnContext, TestContext.Current.CancellationToken);

        receivedEvent.ShouldNotBeNull();
        receivedEvent.Channel.ShouldBe(ChannelType.Teams);
        receivedEvent.EventType.ShouldBe("message");
    }

    private static ITurnContext CreateTurnContext(Activity activity)
    {
        var turnContext = Substitute.For<ITurnContext>();
        turnContext.Activity.Returns(activity);
        turnContext.SendActivityAsync(
                Arg.Any<IActivity>(),
                Arg.Any<CancellationToken>())
            .Returns(new ResourceResponse { Id = "response-1" });
        return turnContext;
    }
}
