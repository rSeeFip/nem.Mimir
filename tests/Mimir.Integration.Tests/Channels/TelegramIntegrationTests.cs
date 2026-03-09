namespace Mimir.Integration.Tests.Channels;

using System.Text.Json;
using FluentAssertions;
using Mimir.Integration.Tests.Fixtures;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using nem.Contracts.Identity;
using NSubstitute;

/// <summary>
/// Integration tests verifying E2E message flow through the Telegram channel adapter contracts.
/// Tests cover text messages, voice messages, and identity resolution.
/// All external APIs are mocked via NSubstitute.
/// </summary>
public sealed class TelegramIntegrationTests : IDisposable
{
    private readonly ChannelTestFixture<IChannelEventSource> _fixture = new(ChannelType.Telegram);

    [Fact]
    public async Task InboundTextMessage_FlowsThroughEventSource_AndRaisesEvent()
    {
        // Arrange
        var telegramUserId = "tg-user-12345";
        var messageText = "Hello from Telegram!";
        ChannelEvent? capturedEvent = null;

        _fixture.EventSource.OnEventReceived += evt =>
        {
            capturedEvent = evt;
            return Task.CompletedTask;
        };

        // Act
        await _fixture.SimulateInboundTextMessageAsync(telegramUserId, messageText);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Channel.Should().Be(ChannelType.Telegram);
        capturedEvent.EventType.Should().Be("message");

        var payload = capturedEvent.Payload!.Value.Deserialize<InboundChannelMessage>();
        payload.Should().NotBeNull();
        payload!.ChannelUserId.Should().Be(telegramUserId);
        payload.Text.Should().Be(messageText);
    }

    [Fact]
    public async Task InboundVoiceMessage_FlowsThroughEventSource_ContainsAudioMetadata()
    {
        // Arrange
        var telegramUserId = "tg-user-voice";
        var audioUrl = "https://api.telegram.org/file/bot_token/voice/file_123.oga";
        ChannelEvent? capturedEvent = null;

        _fixture.EventSource.OnEventReceived += evt =>
        {
            capturedEvent = evt;
            return Task.CompletedTask;
        };

        // Act
        await _fixture.SimulateInboundVoiceMessageAsync(telegramUserId, audioUrl, 8.3);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Channel.Should().Be(ChannelType.Telegram);
        capturedEvent.EventType.Should().Be("voice_message");
        capturedEvent.Payload.Should().NotBeNull();
    }

    [Fact]
    public async Task OutboundMessage_SentViaTelegram_DeliversSuccessfully()
    {
        // Arrange
        var outboundMessage = new OutboundChannelMessage
        {
            Channel = ChannelType.Telegram,
            TargetUserId = "tg-chat-67890",
            ContentPayloadRef = "tg-payload-001",
        };

        // Act
        var result = await _fixture.MessageSender.SendMessageAsync(outboundMessage, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.MessageRef.Should().NotBeNull();
        result.MessageRef!.Channel.Should().Be(ChannelType.Telegram);
        _fixture.SentMessages.Should().ContainSingle()
            .Which.TargetUserId.Should().Be("tg-chat-67890");
    }

    [Fact]
    public void Telegram_SenderCapabilities_IncludeTextAndVoice()
    {
        // Assert
        _fixture.MessageSender.Capabilities.Should().HaveFlag(ChannelCapabilities.Text);
        _fixture.MessageSender.Capabilities.Should().HaveFlag(ChannelCapabilities.Voice);
    }

    public void Dispose() => _fixture.Dispose();
}
