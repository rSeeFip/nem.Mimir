namespace nem.Mimir.Integration.Tests.Channels;

using System.Text.Json;
using FluentAssertions;
using nem.Mimir.Integration.Tests.Fixtures;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using nem.Contracts.Identity;
using NSubstitute;

/// <summary>
/// Integration tests verifying E2E message flow through the WhatsApp channel adapter contracts.
/// All external APIs are mocked via NSubstitute.
/// </summary>
public sealed class WhatsAppIntegrationTests : IDisposable
{
    private readonly ChannelTestFixture<IChannelEventSource> _fixture = new(ChannelType.WhatsApp);

    [Fact]
    public async Task InboundTextMessage_FlowsThroughEventSource_AndRaisesEvent()
    {
        // Arrange
        var phoneNumber = "+1234567890";
        var messageText = "Hello from WhatsApp!";
        ChannelEvent? capturedEvent = null;

        _fixture.EventSource.OnEventReceived += evt =>
        {
            capturedEvent = evt;
            return Task.CompletedTask;
        };

        // Act
        await _fixture.SimulateInboundTextMessageAsync(phoneNumber, messageText);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Channel.Should().Be(ChannelType.WhatsApp);
        capturedEvent.EventType.Should().Be("message");

        var payload = capturedEvent.Payload!.Value.Deserialize<InboundChannelMessage>();
        payload.Should().NotBeNull();
        payload!.ChannelUserId.Should().Be(phoneNumber);
        payload.Text.Should().Be(messageText);
    }

    [Fact]
    public async Task InboundVoiceMessage_FlowsThroughEventSource_WithAudioMetadata()
    {
        // Arrange
        var phoneNumber = "+9876543210";
        var audioUrl = "https://whatsapp.example.com/media/audio123.ogg";
        ChannelEvent? capturedEvent = null;

        _fixture.EventSource.OnEventReceived += evt =>
        {
            capturedEvent = evt;
            return Task.CompletedTask;
        };

        // Act
        await _fixture.SimulateInboundVoiceMessageAsync(phoneNumber, audioUrl, 15.5);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Channel.Should().Be(ChannelType.WhatsApp);
        capturedEvent.EventType.Should().Be("voice_message");
    }

    [Fact]
    public async Task OutboundMessage_SentToPhoneNumber_DeliversSuccessfully()
    {
        // Arrange
        var outboundMessage = new OutboundChannelMessage
        {
            Channel = ChannelType.WhatsApp,
            TargetUserId = "+1234567890",
            ContentPayloadRef = "wa-payload-001",
        };

        // Act
        var result = await _fixture.MessageSender.SendMessageAsync(outboundMessage, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.MessageRef.Should().NotBeNull();
        result.MessageRef!.Channel.Should().Be(ChannelType.WhatsApp);
    }

    [Fact]
    public async Task WhatsApp_PhoneBasedIdentityResolution_ReturnsLinkedIdentity()
    {
        // Arrange
        var phoneNumber = "+1555000111";
        var identity = _fixture.CreateActorIdentity(phoneNumber, "WhatsApp User");
        _fixture.SetupIdentityResolution(phoneNumber, identity);

        // Act
        var resolved = await _fixture.IdentityResolver.ResolveAsync(
            ChannelType.WhatsApp, phoneNumber, CancellationToken.None);

        // Assert
        resolved.Should().NotBeNull();
        resolved!.DisplayName.Should().Be("WhatsApp User");
        resolved.Links.Should().ContainSingle(l =>
            l.ChannelType == ChannelType.WhatsApp && l.ProviderUserId == phoneNumber);
    }

    public void Dispose() => _fixture.Dispose();
}
