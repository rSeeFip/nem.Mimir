namespace Mimir.Integration.Tests.Channels;

using System.Text.Json;
using FluentAssertions;
using Mimir.Integration.Tests.Fixtures;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using nem.Contracts.Identity;
using NSubstitute;

/// <summary>
/// Integration tests verifying E2E message flow through the Teams channel adapter contracts.
/// All external APIs are mocked via NSubstitute.
/// </summary>
public sealed class TeamsIntegrationTests : IDisposable
{
    private readonly ChannelTestFixture<IChannelEventSource> _fixture = new(ChannelType.Teams);

    [Fact]
    public async Task InboundTextMessage_FlowsThroughEventSource_AndRaisesEvent()
    {
        // Arrange
        var userId = "teams-user-001";
        var messageText = "Hello from Teams!";
        ChannelEvent? capturedEvent = null;

        _fixture.EventSource.OnEventReceived += evt =>
        {
            capturedEvent = evt;
            return Task.CompletedTask;
        };

        // Act
        await _fixture.SimulateInboundTextMessageAsync(userId, messageText);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Channel.Should().Be(ChannelType.Teams);
        capturedEvent.EventType.Should().Be("message");
        capturedEvent.Payload.Should().NotBeNull();

        var payload = capturedEvent.Payload!.Value.Deserialize<InboundChannelMessage>();
        payload.Should().NotBeNull();
        payload!.ChannelUserId.Should().Be(userId);
        payload.Text.Should().Be(messageText);
        payload.Channel.Should().Be(ChannelType.Teams);
    }

    [Fact]
    public async Task OutboundTextMessage_SentViaMessageSender_ReturnsSuccessDelivery()
    {
        // Arrange
        var targetUserId = "teams-user-002";
        var outboundMessage = new OutboundChannelMessage
        {
            Channel = ChannelType.Teams,
            TargetUserId = targetUserId,
            ContentPayloadRef = "payload-ref-123",
        };

        // Act
        var result = await _fixture.MessageSender.SendMessageAsync(outboundMessage, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.MessageRef.Should().NotBeNull();
        result.MessageRef!.Channel.Should().Be(ChannelType.Teams);
        _fixture.SentMessages.Should().ContainSingle()
            .Which.TargetUserId.Should().Be(targetUserId);
    }

    [Fact]
    public void Teams_AdapterRegistryResolvesCorrectSender()
    {
        // Act
        var sender = _fixture.AdapterRegistry.GetSender(ChannelType.Teams);

        // Assert
        sender.Should().NotBeNull();
        sender!.Channel.Should().Be(ChannelType.Teams);
        sender.Capabilities.Should().HaveFlag(ChannelCapabilities.Text);
    }

    [Fact]
    public async Task Teams_IdentityResolution_ExistingUser_ReturnsActorIdentity()
    {
        // Arrange
        var providerUserId = "aad-user-123";
        var identity = _fixture.CreateActorIdentity(providerUserId, "Alice (Teams)");
        _fixture.SetupIdentityResolution(providerUserId, identity);

        // Act
        var resolved = await _fixture.IdentityResolver.ResolveAsync(
            ChannelType.Teams, providerUserId, CancellationToken.None);

        // Assert
        resolved.Should().NotBeNull();
        resolved!.DisplayName.Should().Be("Alice (Teams)");
        resolved.Links.Should().ContainSingle(link =>
            link.ChannelType == ChannelType.Teams && link.ProviderUserId == providerUserId);
    }

    [Fact]
    public async Task Teams_FullRoundTrip_InboundEvent_ToIdentityResolution_ToOutboundReply()
    {
        // Arrange
        var userId = "teams-rt-user";
        var identity = _fixture.CreateActorIdentity(userId, "Round Trip User");
        _fixture.SetupIdentityResolution(userId, identity);

        // Act — Step 1: Simulate inbound message
        await _fixture.SimulateInboundTextMessageAsync(userId, "Test round trip");

        // Act — Step 2: Resolve identity from inbound user
        var resolvedIdentity = await _fixture.IdentityResolver.ResolveAsync(
            ChannelType.Teams, userId, CancellationToken.None);

        // Act — Step 3: Send reply via message sender
        var reply = new OutboundChannelMessage
        {
            Channel = ChannelType.Teams,
            TargetUserId = userId,
            ContentPayloadRef = "reply-payload-456",
        };
        var deliveryResult = await _fixture.MessageSender.SendMessageAsync(reply, CancellationToken.None);

        // Assert
        _fixture.ReceivedEvents.Should().HaveCount(1);
        resolvedIdentity.Should().NotBeNull();
        resolvedIdentity!.InternalUserId.Should().NotBeEmpty();
        deliveryResult.Success.Should().BeTrue();
        _fixture.SentMessages.Should().ContainSingle();
    }

    public void Dispose() => _fixture.Dispose();
}
