namespace Mimir.Integration.Tests.Channels;

using System.Text.Json;
using FluentAssertions;
using Mimir.Integration.Tests.Fixtures;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using nem.Contracts.Identity;
using NSubstitute;

/// <summary>
/// Integration tests verifying E2E message flow through the Signal channel adapter contracts.
/// All external APIs are mocked via NSubstitute.
/// </summary>
public sealed class SignalIntegrationTests : IDisposable
{
    private readonly ChannelTestFixture<IChannelEventSource> _fixture = new(ChannelType.Signal);

    [Fact]
    public async Task InboundTextMessage_FlowsThroughEventSource_AndRaisesEvent()
    {
        // Arrange
        var signalUserId = "+1555222333";
        var messageText = "Hello from Signal!";
        ChannelEvent? capturedEvent = null;

        _fixture.EventSource.OnEventReceived += evt =>
        {
            capturedEvent = evt;
            return Task.CompletedTask;
        };

        // Act
        await _fixture.SimulateInboundTextMessageAsync(signalUserId, messageText);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Channel.Should().Be(ChannelType.Signal);
        capturedEvent.EventType.Should().Be("message");

        var payload = capturedEvent.Payload!.Value.Deserialize<InboundChannelMessage>();
        payload.Should().NotBeNull();
        payload!.ChannelUserId.Should().Be(signalUserId);
        payload.Text.Should().Be(messageText);
    }

    [Fact]
    public async Task OutboundMessage_SentViaSignal_DeliversSuccessfully()
    {
        // Arrange
        var outboundMessage = new OutboundChannelMessage
        {
            Channel = ChannelType.Signal,
            TargetUserId = "+1555444666",
            ContentPayloadRef = "signal-payload-001",
        };

        // Act
        var result = await _fixture.MessageSender.SendMessageAsync(outboundMessage, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.MessageRef.Should().NotBeNull();
        result.MessageRef!.Channel.Should().Be(ChannelType.Signal);
    }

    [Fact]
    public async Task Signal_EventSourceReportsCorrectChannel()
    {
        // Assert
        _fixture.EventSource.Channel.Should().Be(ChannelType.Signal);

        // Verify start/stop lifecycle
        await _fixture.EventSource.StartAsync(CancellationToken.None);
        await _fixture.EventSource.StopAsync(CancellationToken.None);

        await _fixture.EventSource.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await _fixture.EventSource.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Signal_IdentityResolution_UnknownUser_ReturnsNull()
    {
        // Arrange
        var unknownUserId = "+1555999000";
        _fixture.SetupUnknownUser(unknownUserId);

        // Act
        var resolved = await _fixture.IdentityResolver.ResolveAsync(
            ChannelType.Signal, unknownUserId, CancellationToken.None);

        // Assert
        resolved.Should().BeNull();
    }

    public void Dispose() => _fixture.Dispose();
}
