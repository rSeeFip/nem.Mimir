namespace Mimir.Integration.Tests.Fixtures;

using System.Text.Json;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using nem.Contracts.Identity;

/// <summary>
/// Generic test fixture that provides a pre-configured mock environment for testing
/// channel adapter integration flows. <typeparamref name="TAdapter"/> represents the
/// adapter type under test (typically IChannelEventSource or IChannelMessageSender).
/// </summary>
/// <typeparam name="TAdapter">The channel adapter interface being tested.</typeparam>
public class ChannelTestFixture<TAdapter> : IDisposable where TAdapter : class
{
    /// <summary>The channel type this fixture targets.</summary>
    public ChannelType Channel { get; }

    /// <summary>Mock event source for simulating inbound channel events.</summary>
    public IChannelEventSource EventSource { get; }

    /// <summary>Mock message sender for verifying outbound message delivery.</summary>
    public IChannelMessageSender MessageSender { get; }

    /// <summary>Mock adapter registry for resolving adapters by channel type.</summary>
    public IChannelAdapterRegistry AdapterRegistry { get; }

    /// <summary>Mock identity resolver for resolving ActorIdentity from channel user IDs.</summary>
    public IActorIdentityResolver IdentityResolver { get; }

    /// <summary>Mock identity service for managing channel identity links.</summary>
    public IActorIdentityService IdentityService { get; }

    /// <summary>Collected events raised by the event source during tests.</summary>
    public List<ChannelEvent> ReceivedEvents { get; } = [];

    /// <summary>Collected outbound messages sent via the message sender during tests.</summary>
    public List<OutboundChannelMessage> SentMessages { get; } = [];

    private Func<ChannelEvent, Task>? _eventHandler;

    public ChannelTestFixture(ChannelType channel)
    {
        Channel = channel;

        // Configure mock event source
        EventSource = Substitute.For<IChannelEventSource>();
        EventSource.Channel.Returns(channel);
        EventSource.When(x => x.OnEventReceived += Arg.Any<Func<ChannelEvent, Task>>())
            .Do(callInfo => _eventHandler = callInfo.Arg<Func<ChannelEvent, Task>>());

        // Configure mock message sender
        MessageSender = Substitute.For<IChannelMessageSender>();
        MessageSender.Channel.Returns(channel);
        MessageSender.Capabilities.Returns(
            ChannelCapabilities.Text | ChannelCapabilities.Voice | ChannelCapabilities.Images);
        MessageSender.SendMessageAsync(Arg.Any<OutboundChannelMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var msg = callInfo.Arg<OutboundChannelMessage>();
                SentMessages.Add(msg);
                return new MessageDeliveryResult
                {
                    Success = true,
                    MessageRef = new ChannelMessageRef(channel, Guid.NewGuid().ToString()),
                };
            });

        // Configure mock adapter registry
        AdapterRegistry = Substitute.For<IChannelAdapterRegistry>();
        AdapterRegistry.GetSender(channel).Returns(MessageSender);
        AdapterRegistry.GetEventSource(channel).Returns(EventSource);
        AdapterRegistry.SupportedChannels.Returns([channel]);

        // Configure mock identity resolver
        IdentityResolver = Substitute.For<IActorIdentityResolver>();

        // Configure mock identity service
        IdentityService = Substitute.For<IActorIdentityService>();
    }

    /// <summary>
    /// Simulates an inbound text message arriving from the channel.
    /// </summary>
    public async Task SimulateInboundTextMessageAsync(string userId, string text)
    {
        var inboundMessage = new InboundChannelMessage
        {
            Channel = Channel,
            ChannelUserId = userId,
            Text = text,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var channelEvent = new ChannelEvent
        {
            Channel = Channel,
            EventType = "message",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.SerializeToElement(inboundMessage),
        };

        ReceivedEvents.Add(channelEvent);

        if (_eventHandler is not null)
        {
            await _eventHandler(channelEvent);
        }
    }

    /// <summary>
    /// Simulates an inbound voice message arriving from the channel.
    /// </summary>
    public async Task SimulateInboundVoiceMessageAsync(string userId, string audioUrl, double durationSeconds)
    {
        var voiceContent = new VoiceContent(null, audioUrl, durationSeconds, "audio/ogg");

        var channelEvent = new ChannelEvent
        {
            Channel = Channel,
            EventType = "voice_message",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.SerializeToElement(new
            {
                channel = Channel.ToString(),
                channelUserId = userId,
                audioUrl,
                durationSeconds,
                mimeType = "audio/ogg",
            }),
        };

        ReceivedEvents.Add(channelEvent);

        if (_eventHandler is not null)
        {
            await _eventHandler(channelEvent);
        }
    }

    /// <summary>
    /// Creates a standard text content payload for testing.
    /// </summary>
    public static TextContent CreateTextContent(string text) =>
        new(text) { CreatedAt = DateTimeOffset.UtcNow };

    /// <summary>
    /// Creates a standard voice content payload for testing.
    /// </summary>
    public static VoiceContent CreateVoiceContent(
        string? transcription = null,
        string? audioUrl = null,
        double? duration = null) =>
        new(transcription, audioUrl, duration, "audio/ogg") { CreatedAt = DateTimeOffset.UtcNow };

    /// <summary>
    /// Creates a standard ActorIdentity with a single channel link.
    /// </summary>
    public ActorIdentity CreateActorIdentity(string providerUserId, string displayName = "Test User")
    {
        return new ActorIdentity
        {
            InternalUserId = Guid.NewGuid(),
            DisplayName = displayName,
            Links =
            [
                new ChannelIdentityLink
                {
                    ChannelType = Channel,
                    ProviderUserId = providerUserId,
                    TrustLevel = TrustLevel.Verified,
                    IsActive = true,
                },
            ],
        };
    }

    /// <summary>
    /// Configures the identity resolver to return the given identity for a provider user ID.
    /// </summary>
    public void SetupIdentityResolution(string providerUserId, ActorIdentity identity)
    {
        IdentityResolver
            .ResolveAsync(Channel, providerUserId, Arg.Any<CancellationToken>())
            .Returns(identity);
    }

    /// <summary>
    /// Configures the identity resolver to return null (unknown user).
    /// </summary>
    public void SetupUnknownUser(string providerUserId)
    {
        IdentityResolver
            .ResolveAsync(Channel, providerUserId, Arg.Any<CancellationToken>())
            .Returns((ActorIdentity?)null);
    }

    public void Dispose()
    {
        ReceivedEvents.Clear();
        SentMessages.Clear();
        GC.SuppressFinalize(this);
    }
}
