namespace Mimir.WhatsApp.Tests.Services;

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mimir.Application.ChannelEvents;
using Mimir.WhatsApp.Configuration;
using Mimir.WhatsApp.Models;
using Mimir.WhatsApp.Services;
using nem.Contracts.Channels;
using NSubstitute;
using Shouldly;

public sealed class WhatsAppChannelAdapterTests
{
    private readonly WhatsAppSettings _settings = new()
    {
        AccessToken = "test-token",
        PhoneNumberId = "123456",
        VerifyToken = "verify-me",
        AppSecret = "app-secret",
        ApiBaseUrl = "https://graph.facebook.com/v21.0",
    };

    private WhatsAppChannelAdapter CreateAdapter(ISender? mediator = null, IHttpClientFactory? httpClientFactory = null)
    {
        mediator ??= Substitute.For<ISender>();
        mediator.Send(Arg.Any<IngestChannelEventCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelEventResult(Guid.NewGuid(), true));

        httpClientFactory ??= CreateStubHttpClientFactory();

        return new WhatsAppChannelAdapter(
            Options.Create(_settings),
            httpClientFactory,
            mediator,
            NullLogger<WhatsAppChannelAdapter>.Instance);
    }

    [Fact]
    public void Channel_ReturnsWhatsApp()
    {
        var adapter = CreateAdapter();
        adapter.Channel.ShouldBe(ChannelType.WhatsApp);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyAccessToken_ReturnsEarly()
    {
        var settings = new WhatsAppSettings { AccessToken = "" };
        var adapter = new WhatsAppChannelAdapter(
            Options.Create(settings),
            CreateStubHttpClientFactory(),
            Substitute.For<ISender>(),
            NullLogger<WhatsAppChannelAdapter>.Instance);

        await adapter.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100);
        await adapter.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessWebhookPayloadAsync_TextMessage_DispatchesIngestCommand()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<IngestChannelEventCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelEventResult(Guid.NewGuid(), true));
        var adapter = CreateAdapter(mediator);

        var payload = CreateTextMessagePayload("15551234567", "Hello!");

        // Act
        await adapter.ProcessWebhookPayloadAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<IngestChannelEventCommand>(cmd =>
                cmd.Channel == ChannelType.WhatsApp &&
                cmd.ExternalUserId == "15551234567"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessWebhookPayloadAsync_NullEntry_DoesNothing()
    {
        var mediator = Substitute.For<ISender>();
        var adapter = CreateAdapter(mediator);

        var payload = new WhatsAppWebhookPayload { Object = "whatsapp_business_account", Entry = null };

        await adapter.ProcessWebhookPayloadAsync(payload, TestContext.Current.CancellationToken);

        await mediator.DidNotReceive().Send(Arg.Any<IngestChannelEventCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessWebhookPayloadAsync_StatusUpdate_DoesNotDispatch()
    {
        var mediator = Substitute.For<ISender>();
        var adapter = CreateAdapter(mediator);

        var payload = new WhatsAppWebhookPayload
        {
            Object = "whatsapp_business_account",
            Entry =
            [
                new WhatsAppEntry
                {
                    Id = "entry-1",
                    Changes =
                    [
                        new WhatsAppChange
                        {
                            Field = "messages",
                            Value = new WhatsAppChangeValue
                            {
                                MessagingProduct = "whatsapp",
                                Statuses = [new WhatsAppStatus { Id = "status-1", Status = "delivered" }],
                            },
                        },
                    ],
                },
            ],
        };

        await adapter.ProcessWebhookPayloadAsync(payload, TestContext.Current.CancellationToken);

        await mediator.DidNotReceive().Send(Arg.Any<IngestChannelEventCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_TextContent_PostsToWhatsAppApi()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            JsonSerializer.Serialize(new WhatsAppSendMessageResponse
            {
                MessagingProduct = "whatsapp",
                Messages = [new WhatsAppSendMessageRef { Id = "wamid.abc123" }],
            }));

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(WhatsAppChannelAdapter.HttpClientName)
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com") });

        var adapter = CreateAdapter(httpClientFactory: factory);

        var content = new nem.Contracts.Content.TextContent("Hello from Mimir!");

        // Act
        var result = await adapter.SendAsync("15551234567", content, TestContext.Current.CancellationToken);

        // Assert
        result.Success.ShouldBeTrue();
        result.MessageRef.ShouldNotBeNull();
        result.MessageRef.ExternalMessageId.ShouldBe("wamid.abc123");
        result.MessageRef.Channel.ShouldBe(ChannelType.WhatsApp);
    }

    [Fact]
    public async Task SendAsync_NonTextContent_ReturnsFailure()
    {
        var adapter = CreateAdapter();
        var content = new nem.Contracts.Content.VoiceContent(null, null, null, null);

        var result = await adapter.SendAsync("15551234567", content, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("text messages");
    }

    [Fact]
    public async Task SendAsync_ApiError_ReturnsFailure()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "");
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(WhatsAppChannelAdapter.HttpClientName)
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com") });

        var adapter = CreateAdapter(httpClientFactory: factory);
        var content = new nem.Contracts.Content.TextContent("Hello");

        var result = await adapter.SendAsync("15551234567", content, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    private static WhatsAppWebhookPayload CreateTextMessagePayload(string from, string text) =>
        new()
        {
            Object = "whatsapp_business_account",
            Entry =
            [
                new WhatsAppEntry
                {
                    Id = "entry-1",
                    Changes =
                    [
                        new WhatsAppChange
                        {
                            Field = "messages",
                            Value = new WhatsAppChangeValue
                            {
                                MessagingProduct = "whatsapp",
                                Metadata = new WhatsAppMetadata { PhoneNumberId = "123456" },
                                Contacts = [new WhatsAppContact { WaId = from, Profile = new WhatsAppProfile { Name = "Test User" } }],
                                Messages =
                                [
                                    new WhatsAppMessage
                                    {
                                        From = from,
                                        Id = "wamid.test-msg-1",
                                        Timestamp = "1709740800",
                                        Type = "text",
                                        Text = new WhatsAppTextBody { Body = text },
                                    },
                                ],
                            },
                        },
                    ],
                },
            ],
        };

    private static IHttpClientFactory CreateStubHttpClientFactory()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
            .Returns(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"))
            {
                BaseAddress = new Uri("https://graph.facebook.com"),
            });
        return factory;
    }

    internal sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            });
        }
    }
}
