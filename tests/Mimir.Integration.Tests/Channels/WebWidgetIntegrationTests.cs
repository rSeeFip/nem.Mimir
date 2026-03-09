namespace Mimir.Integration.Tests.Channels;

using System.Text.Json;
using FluentAssertions;
using Mimir.Integration.Tests.Fixtures;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using nem.Contracts.Identity;
using NSubstitute;

/// <summary>
/// Integration tests verifying E2E message flow through the WebWidget channel adapter contracts.
/// Tests cover text messages, streaming capabilities, and identity resolution.
/// All external APIs are mocked via NSubstitute.
/// </summary>
public sealed class WebWidgetIntegrationTests : IDisposable
{
    private readonly ChannelTestFixture<IChannelEventSource> _fixture = new(ChannelType.WebWidget);

    [Fact]
    public async Task InboundTextMessage_FlowsThroughEventSource_AndRaisesEvent()
    {
        // Arrange
        var widgetUserId = "web-user-abc123";
        var messageText = "Hello from WebWidget!";
        ChannelEvent? capturedEvent = null;

        _fixture.EventSource.OnEventReceived += evt =>
        {
            capturedEvent = evt;
            return Task.CompletedTask;
        };

        // Act
        await _fixture.SimulateInboundTextMessageAsync(widgetUserId, messageText);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Channel.Should().Be(ChannelType.WebWidget);
        capturedEvent.EventType.Should().Be("message");

        var payload = capturedEvent.Payload!.Value.Deserialize<InboundChannelMessage>();
        payload.Should().NotBeNull();
        payload!.ChannelUserId.Should().Be(widgetUserId);
        payload.Text.Should().Be(messageText);
    }

    [Fact]
    public async Task OutboundMessage_SentViaWebWidget_DeliversSuccessfully()
    {
        // Arrange
        var outboundMessage = new OutboundChannelMessage
        {
            Channel = ChannelType.WebWidget,
            TargetUserId = "web-user-xyz789",
            ContentPayloadRef = "widget-payload-001",
        };

        // Act
        var result = await _fixture.MessageSender.SendMessageAsync(outboundMessage, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.MessageRef.Should().NotBeNull();
        result.MessageRef!.Channel.Should().Be(ChannelType.WebWidget);
    }

    [Fact]
    public void WebWidget_SupportsStreamingCapability()
    {
        // Arrange — configure WebWidget with streaming support
        var streamingSender = Substitute.For<IChannelMessageSender>();
        streamingSender.Channel.Returns(ChannelType.WebWidget);
        streamingSender.Capabilities.Returns(
            ChannelCapabilities.Text | ChannelCapabilities.StreamingUpdates);

        // Assert
        streamingSender.Capabilities.Should().HaveFlag(ChannelCapabilities.StreamingUpdates);
        streamingSender.Capabilities.Should().HaveFlag(ChannelCapabilities.Text);
    }

    [Fact]
    public async Task WebWidget_IdentityResolution_JwtBasedUser_ReturnsIdentity()
    {
        // Arrange
        var jwtUserId = "jwt-web-user-001";
        var identity = _fixture.CreateActorIdentity(jwtUserId, "WebWidget User");
        _fixture.SetupIdentityResolution(jwtUserId, identity);

        // Act
        var resolved = await _fixture.IdentityResolver.ResolveAsync(
            ChannelType.WebWidget, jwtUserId, CancellationToken.None);

        // Assert
        resolved.Should().NotBeNull();
        resolved!.DisplayName.Should().Be("WebWidget User");
        resolved.Links.Should().ContainSingle(l =>
            l.ChannelType == ChannelType.WebWidget && l.ProviderUserId == jwtUserId);
    }

    public void Dispose() => _fixture.Dispose();
}
