namespace Mimir.Signal.Tests.Services;

using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Signal.Configuration;
using Mimir.Signal.Services;
using NSubstitute;
using Shouldly;

public sealed class SignalApiClientTests
{
    private readonly SignalSettings _settings = new()
    {
        ApiBaseUrl = "http://localhost:8080",
        PhoneNumber = "+1234567890",
    };

    private SignalApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(_settings.ApiBaseUrl) };
        var options = Substitute.For<IOptions<SignalSettings>>();
        options.Value.Returns(_settings);
        var logger = Substitute.For<ILogger<SignalApiClient>>();
        return new SignalApiClient(httpClient, options, logger);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_ReturnsMessages_WhenApiReturnsData()
    {
        var responseJson = """
        [
          {
            "envelope": {
              "source": "+9876543210",
              "sourceUuid": "uuid-sender-1",
              "timestamp": 1700000000000,
              "dataMessage": {
                "message": "Hello from Signal",
                "timestamp": 1700000000000
              }
            }
          }
        ]
        """;

        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);
        var messages = await client.ReceiveMessagesAsync(TestContext.Current.CancellationToken);

        messages.Count.ShouldBe(1);
        messages[0].Envelope.ShouldNotBeNull();
        messages[0].Envelope!.SourceUuid.ShouldBe("uuid-sender-1");
        messages[0].Envelope!.DataMessage.ShouldNotBeNull();
        messages[0].Envelope!.DataMessage!.Message.ShouldBe("Hello from Signal");
    }

    [Fact]
    public async Task ReceiveMessagesAsync_ReturnsEmpty_WhenApiReturnsEmptyArray()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);
        var messages = await client.ReceiveMessagesAsync(TestContext.Current.CancellationToken);

        messages.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_UsesEncodedPhoneNumber()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);
        await client.ReceiveMessagesAsync(TestContext.Current.CancellationToken);

        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri!.PathAndQuery.ShouldContain("/v1/receive/");
        handler.LastRequestUri!.PathAndQuery.ShouldContain("%2B1234567890");
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsTrue_OnSuccess()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Created));
        var client = CreateClient(handler);

        var result = await client.SendMessageAsync("+9876543210", "Test message", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri!.PathAndQuery.ShouldBe("/v2/send");
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFalse_OnFailure()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var result = await client.SendMessageAsync("+9876543210", "Test message", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsTrue_WhenApiReachable()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        var result = await client.CheckHealthAsync(TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        handler.LastRequestUri!.PathAndQuery.ShouldBe("/v1/about");
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsFalse_WhenApiUnreachable()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = CreateClient(handler);

        var result = await client.CheckHealthAsync(TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsFalse_OnException()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        var result = await client.CheckHealthAsync(TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ReceiveMessagesAsync_ParsesAttachments()
    {
        var responseJson = """
        [
          {
            "envelope": {
              "source": "+9876543210",
              "sourceUuid": "uuid-sender-1",
              "timestamp": 1700000000000,
              "dataMessage": {
                "message": "See attached",
                "timestamp": 1700000000000,
                "attachments": [
                  {
                    "contentType": "image/png",
                    "filename": "photo.png",
                    "id": "att-1",
                    "size": 1024
                  }
                ]
              }
            }
          }
        ]
        """;

        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);
        var messages = await client.ReceiveMessagesAsync(TestContext.Current.CancellationToken);

        messages.Count.ShouldBe(1);
        var dataMsg = messages[0].Envelope!.DataMessage!;
        dataMsg.Attachments.ShouldNotBeNull();
        dataMsg.Attachments!.Count.ShouldBe(1);
        dataMsg.Attachments[0].ContentType.ShouldBe("image/png");
        dataMsg.Attachments[0].Filename.ShouldBe("photo.png");
        dataMsg.Attachments[0].Id.ShouldBe("att-1");
        dataMsg.Attachments[0].Size.ShouldBe(1024);
    }
}
