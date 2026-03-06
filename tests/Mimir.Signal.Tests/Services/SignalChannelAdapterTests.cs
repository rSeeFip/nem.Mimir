namespace Mimir.Signal.Tests.Services;

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Signal.Configuration;
using Mimir.Signal.Services;
using nem.Contracts.Channels;
using NSubstitute;
using Shouldly;

public sealed class SignalChannelAdapterTests
{
    private readonly SignalSettings _settings = new()
    {
        PhoneNumber = "+1234567890",
        ApiBaseUrl = "http://localhost:8080",
        PollingIntervalMs = 50,
    };

    [Fact]
    public void Channel_ReturnsSignal()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        adapter.Channel.ShouldBe(ChannelType.Signal);
    }

    [Fact]
    public void Capabilities_IncludesTextAndAttachments()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        adapter.Capabilities.HasFlag(ChannelCapabilities.Text).ShouldBeTrue();
        adapter.Capabilities.HasFlag(ChannelCapabilities.FileAttachments).ShouldBeTrue();
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsSuccess_WhenApiSucceeds()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Created));
        var adapter = CreateAdapter(handler);

        var outbound = new OutboundChannelMessage
        {
            Channel = ChannelType.Signal,
            TargetUserId = "+9876543210",
            ContentPayloadRef = "Hello back!",
        };

        var result = await adapter.SendMessageAsync(outbound, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.MessageRef.ShouldNotBeNull();
        result.MessageRef!.Channel.ShouldBe(ChannelType.Signal);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFailure_WhenApiFails()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var adapter = CreateAdapter(handler);

        var outbound = new OutboundChannelMessage
        {
            Channel = ChannelType.Signal,
            TargetUserId = "+9876543210",
            ContentPayloadRef = "Hello back!",
        };

        var result = await adapter.SendMessageAsync(outbound, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotStart_WhenPhoneNumberMissing()
    {
        var emptySettings = new SignalSettings { PhoneNumber = "" };
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = CreateAdapter(handler, emptySettings);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await adapter.StartAsync(cts.Token);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await adapter.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_RaisesOnEventReceived_WhenMessageReceived()
    {
        var receiveResponse = """
        [
          {
            "envelope": {
              "source": "+9876543210",
              "sourceUuid": "uuid-sender-1",
              "timestamp": 1700000000000,
              "dataMessage": {
                "message": "Incoming Signal message",
                "timestamp": 1700000000000
              }
            }
          }
        ]
        """;

        var callCount = 0;
        var handler = new SequencingHttpMessageHandler([
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(receiveResponse, System.Text.Encoding.UTF8, "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            },
        ]);

        var adapter = CreateAdapter(handler);
        ChannelEvent? receivedEvent = null;

        adapter.OnEventReceived += evt =>
        {
            receivedEvent = evt;
            callCount++;
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await adapter.StartAsync(cts.Token);

        var maxWait = 50;
        while (receivedEvent is null && maxWait-- > 0)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        await adapter.StopAsync(CancellationToken.None);

        receivedEvent.ShouldNotBeNull();
        receivedEvent!.Channel.ShouldBe(ChannelType.Signal);
        receivedEvent.EventType.ShouldBe("message");
        callCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesAttachmentMetadata_InEvent()
    {
        var receiveResponse = """
        [
          {
            "envelope": {
              "source": "+9876543210",
              "sourceUuid": "uuid-sender-1",
              "timestamp": 1700000000000,
              "dataMessage": {
                "message": "Check this out",
                "timestamp": 1700000000000,
                "attachments": [
                  {
                    "contentType": "image/jpeg",
                    "filename": "photo.jpg",
                    "id": "att-42",
                    "size": 2048
                  }
                ]
              }
            }
          }
        ]
        """;

        var handler = new SequencingHttpMessageHandler([
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(receiveResponse, System.Text.Encoding.UTF8, "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            },
        ]);

        var adapter = CreateAdapter(handler);
        ChannelEvent? receivedEvent = null;

        adapter.OnEventReceived += evt =>
        {
            receivedEvent = evt;
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await adapter.StartAsync(cts.Token);

        var maxWait = 50;
        while (receivedEvent is null && maxWait-- > 0)
            await Task.Delay(50, TestContext.Current.CancellationToken);

        await adapter.StopAsync(CancellationToken.None);

        receivedEvent.ShouldNotBeNull();
        receivedEvent!.Payload.ShouldNotBeNull();

        var payload = receivedEvent.Payload!.Value;
        var inbound = JsonSerializer.Deserialize<InboundChannelMessage>(payload.GetRawText());
        inbound.ShouldNotBeNull();
        inbound!.ChannelUserId.ShouldBe("uuid-sender-1");
        inbound.Metadata.ShouldNotBeNull();
        inbound.Metadata!["signal.attachment_count"].ShouldBe("1");
        inbound.Metadata["signal.attachment_0_type"].ShouldBe("image/jpeg");
        inbound.Metadata["signal.attachment_0_name"].ShouldBe("photo.jpg");
    }

    [Fact]
    public void IChannelMessageSender_Channel_ReturnsSignal()
    {
        var adapter = CreateAdapter(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        IChannelEventSource source = adapter;
        source.Channel.ShouldBe(ChannelType.Signal);
    }

    private SignalChannelAdapter CreateAdapter(HttpMessageHandler handler, SignalSettings? settings = null)
    {
        var effectiveSettings = settings ?? _settings;
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(effectiveSettings.ApiBaseUrl) };
        var options = Substitute.For<IOptions<SignalSettings>>();
        options.Value.Returns(effectiveSettings);
        var apiClientLogger = Substitute.For<ILogger<SignalApiClient>>();
        var apiClient = new SignalApiClient(httpClient, options, apiClientLogger);
        var adapterLogger = Substitute.For<ILogger<SignalChannelAdapter>>();
        return new SignalChannelAdapter(apiClient, options, adapterLogger);
    }
}
