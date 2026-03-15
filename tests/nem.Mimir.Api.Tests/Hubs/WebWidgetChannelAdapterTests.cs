using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using nem.Mimir.Api.Hubs;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Api.Tests.Hubs;

public sealed class WebWidgetChannelAdapterTests : IDisposable
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<WebWidgetChannelAdapter> _logger;
    private readonly WebWidgetChannelAdapter _adapter;

    public WebWidgetChannelAdapterTests()
    {
        _hubContext = Substitute.For<IHubContext<ChatHub>>();
        _logger = NullLogger<WebWidgetChannelAdapter>.Instance;
        _adapter = new WebWidgetChannelAdapter(_hubContext, _logger);
    }

    public void Dispose()
    {
        _adapter.Dispose();
    }

    // ── Channel property ──────────────────────────────────────────────────

    [Fact]
    public void Channel_ReturnsWebWidget()
    {
        _adapter.Channel.ShouldBe(ChannelType.WebWidget);
    }

    [Fact]
    public void Capabilities_ReturnsTextAndStreamingUpdates()
    {
        var expected = ChannelCapabilities.Text | ChannelCapabilities.StreamingUpdates;
        _adapter.Capabilities.ShouldBe(expected);
    }

    // ── IChannelEventSource ──────────────────────────────────────────────

    [Fact]
    public async Task HandleInboundMessage_RaisesOnEventReceived_WithTextContent()
    {
        // Arrange
        ChannelEvent? receivedEvent = null;
        _adapter.OnEventReceived += evt =>
        {
            receivedEvent = evt;
            return Task.CompletedTask;
        };

        // Act
        await _adapter.HandleInboundMessage(
            connectionId: "conn-1",
            userId: "user-123",
            conversationId: "conv-456",
            text: "Hello world");

        // Assert
        receivedEvent.ShouldNotBeNull();
        receivedEvent.Channel.ShouldBe(ChannelType.WebWidget);
        receivedEvent.EventType.ShouldBe("message");
        receivedEvent.Payload.ShouldNotBeNull();

        var payload = receivedEvent.Payload.Value;
        var inbound = JsonSerializer.Deserialize<InboundChannelMessage>(payload.GetRawText());
        inbound.ShouldNotBeNull();
        inbound.Channel.ShouldBe(ChannelType.WebWidget);
        inbound.ChannelUserId.ShouldBe("user-123");
        inbound.Text.ShouldBe("Hello world");
    }

    [Fact]
    public async Task HandleInboundMessage_NoSubscribers_DoesNotThrow()
    {
        await _adapter.HandleInboundMessage("conn-1", "user-1", "conv-1", "test");
    }

    [Fact]
    public async Task HandleInboundMessage_IncludesMetadata()
    {
        // Arrange
        ChannelEvent? receivedEvent = null;
        _adapter.OnEventReceived += evt =>
        {
            receivedEvent = evt;
            return Task.CompletedTask;
        };

        // Act
        await _adapter.HandleInboundMessage(
            connectionId: "conn-abc",
            userId: "user-xyz",
            conversationId: "conv-999",
            text: "hi");

        // Assert
        receivedEvent.ShouldNotBeNull();
        var payload = JsonSerializer.Deserialize<InboundChannelMessage>(
            receivedEvent.Payload!.Value.GetRawText());
        payload.ShouldNotBeNull();
        payload.Metadata.ShouldNotBeNull();
        payload.Metadata.ContainsKey("webwidget.connection_id").ShouldBeTrue();
        payload.Metadata["webwidget.connection_id"].ShouldBe("conn-abc");
        payload.Metadata.ContainsKey("webwidget.conversation_id").ShouldBeTrue();
        payload.Metadata["webwidget.conversation_id"].ShouldBe("conv-999");
    }

    // ── IChannelMessageSender.SendMessageAsync ──────────────────────────

    [Fact]
    public async Task SendMessageAsync_SendsToTargetUser_ViaSignalR()
    {
        // Arrange
        var mockClients = Substitute.For<IHubClients>();
        var mockClientProxy = Substitute.For<IClientProxy>();
        _hubContext.Clients.Returns(mockClients);
        mockClients.User("user-123").Returns(mockClientProxy);

        var message = new OutboundChannelMessage
        {
            Channel = ChannelType.WebWidget,
            TargetUserId = "user-123",
            ContentPayloadRef = "Hello from server",
        };

        // Act
        var result = await _adapter.SendMessageAsync(message, TestContext.Current.CancellationToken);

        // Assert
        result.Success.ShouldBeTrue();
        result.MessageRef.ShouldNotBeNull();
        result.MessageRef.Channel.ShouldBe(ChannelType.WebWidget);
        await mockClientProxy.Received(1).SendCoreAsync(
            "ReceiveMessage",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFailure_OnException()
    {
        // Arrange
        var mockClients = Substitute.For<IHubClients>();
        var mockClientProxy = Substitute.For<IClientProxy>();
        _hubContext.Clients.Returns(mockClients);
        mockClients.User("user-fail").Returns(mockClientProxy);
        mockClientProxy
            .When(x => x.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>()))
            .Throw(new InvalidOperationException("connection lost"));

        var message = new OutboundChannelMessage
        {
            Channel = ChannelType.WebWidget,
            TargetUserId = "user-fail",
            ContentPayloadRef = "test",
        };

        // Act
        var result = await _adapter.SendMessageAsync(message, TestContext.Current.CancellationToken);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }

    // ── SendStreamingAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SendStreamingAsync_StreamsTokensToClient()
    {
        // Arrange
        var mockClients = Substitute.For<IHubClients>();
        var mockClientProxy = Substitute.For<IClientProxy>();
        _hubContext.Clients.Returns(mockClients);
        mockClients.User("user-stream").Returns(mockClientProxy);

        var message = new OutboundChannelMessage
        {
            Channel = ChannelType.WebWidget,
            TargetUserId = "user-stream",
        };

        var chunks = ToAsyncEnumerable("Hello", " ", "world");

        // Act
        await _adapter.SendStreamingAsync(message, chunks, TestContext.Current.CancellationToken);

        // Assert — each chunk sent as a ChatToken via SignalR
        await mockClientProxy.Received(3).SendCoreAsync(
            "ReceiveToken",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    // ── ActorIdentity from claims ────────────────────────────────────────

    [Fact]
    public void ExtractActorIdentity_FromKeycloakClaims_MapsCorrectly()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "kc-sub-12345"),
            new Claim(ClaimTypes.Email, "user@example.com"),
            new Claim(ClaimTypes.Name, "Test User"),
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var actor = WebWidgetChannelAdapter.ExtractActorIdentity(principal);

        // Assert
        actor.ShouldNotBeNull();
        actor.UserId.ShouldBe("kc-sub-12345");
        actor.Email.ShouldBe("user@example.com");
        actor.DisplayName.ShouldBe("Test User");
    }

    [Fact]
    public void ExtractActorIdentity_WithSubClaim_UsesSubForUserId()
    {
        var claims = new[]
        {
            new Claim("sub", "keycloak-uuid-abc"),
            new Claim(ClaimTypes.Email, "alt@example.com"),
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var actor = WebWidgetChannelAdapter.ExtractActorIdentity(principal);

        actor.ShouldNotBeNull();
        actor.UserId.ShouldBe("keycloak-uuid-abc");
        actor.Email.ShouldBe("alt@example.com");
    }

    [Fact]
    public void ExtractActorIdentity_NullPrincipal_ReturnsNull()
    {
        var actor = WebWidgetChannelAdapter.ExtractActorIdentity(null);
        actor.ShouldBeNull();
    }

    [Fact]
    public void ExtractActorIdentity_NoClaims_ReturnsNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var actor = WebWidgetChannelAdapter.ExtractActorIdentity(principal);
        actor.ShouldBeNull();
    }

    // ── ContentPayload conversion ────────────────────────────────────────

    [Fact]
    public void ConvertToContentPayload_PlainText_ReturnsTextContent()
    {
        var payload = WebWidgetChannelAdapter.ConvertToContentPayload("Hello world");

        payload.ShouldBeOfType<TextContent>();
        var text = (TextContent)payload;
        text.Text.ShouldBe("Hello world");
    }

    [Fact]
    public void ConvertToContentPayload_JsonContentBlocks_DeserializesBlocks()
    {
        var json = """{"contentType":"text","text":"Structured message","blocks":[]}""";
        var payload = WebWidgetChannelAdapter.ConvertToContentPayload(json);

        payload.ShouldBeOfType<TextContent>();
        var text = (TextContent)payload;
        text.Text.ShouldBe("Structured message");
    }

    [Fact]
    public void ConvertToContentPayload_InvalidJson_FallsBackToTextContent()
    {
        var invalidJson = "{ broken json [[[";
        var payload = WebWidgetChannelAdapter.ConvertToContentPayload(invalidJson);

        payload.ShouldBeOfType<TextContent>();
        var text = (TextContent)payload;
        text.Text.ShouldBe(invalidJson);
    }

    [Fact]
    public void ConvertToContentPayload_NullOrEmpty_ReturnsEmptyTextContent()
    {
        var payload = WebWidgetChannelAdapter.ConvertToContentPayload("");
        payload.ShouldBeOfType<TextContent>();
        ((TextContent)payload).Text.ShouldBe(string.Empty);
    }

    // ── ActivitySource ──────────────────────────────────────────────────

    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        WebWidgetActivitySource.Name.ShouldBe("nem.Mimir.WebWidget");
        WebWidgetActivitySource.Instance.Name.ShouldBe("nem.Mimir.WebWidget");
    }

    // ── BackgroundService lifecycle ─────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CompletesOnCancellation()
    {
        using var cts = new CancellationTokenSource();
        var startTask = _adapter.StartAsync(cts.Token);
        await startTask;

        // Give it a moment to enter ExecuteAsync
        await Task.Delay(50);

        // Cancel and stop
        await cts.CancelAsync();
        await _adapter.StopAsync(CancellationToken.None);
    }

    // ── IContentPayload → SignalR message conversion ─────────────────────

    [Fact]
    public void FormatOutboundPayload_TextContent_ReturnsTokenAndBlocks()
    {
        var content = new TextContent("Hello there");
        var (text, blocksJson) = WebWidgetChannelAdapter.FormatOutboundPayload(content);

        text.ShouldBe("Hello there");
        blocksJson.ShouldBeNull();
    }

    [Fact]
    public void FormatOutboundPayload_NullContentPayloadRef_ReturnsEmpty()
    {
        var (text, blocksJson) = WebWidgetChannelAdapter.FormatOutboundPayload((IContentPayload?)null);
        text.ShouldBe(string.Empty);
        blocksJson.ShouldBeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(params string[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
