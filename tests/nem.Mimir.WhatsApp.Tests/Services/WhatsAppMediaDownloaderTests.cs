namespace nem.Mimir.WhatsApp.Tests.Services;

using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using nem.Mimir.WhatsApp.Configuration;
using nem.Mimir.WhatsApp.Services;
using NSubstitute;
using Shouldly;

public sealed class WhatsAppMediaDownloaderTests
{
    private readonly WhatsAppSettings _settings = new()
    {
        AccessToken = "test-token",
        ApiBaseUrl = "https://graph.facebook.com/v21.0",
    };

    [Fact]
    public async Task GetMediaUrlAsync_Success_ReturnsUrl()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            """{"url":"https://cdn.whatsapp.net/media/123","mime_type":"image/jpeg"}""");
        var factory = CreateFactory(handler);
        var downloader = new WhatsAppMediaDownloader(factory, Options.Create(_settings),
            NullLogger<WhatsAppMediaDownloader>.Instance);

        // Act
        var result = await downloader.GetMediaUrlAsync("media-123", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe("https://cdn.whatsapp.net/media/123");
    }

    [Fact]
    public async Task GetMediaUrlAsync_Failure_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "error");
        var factory = CreateFactory(handler);
        var downloader = new WhatsAppMediaDownloader(factory, Options.Create(_settings),
            NullLogger<WhatsAppMediaDownloader>.Instance);

        var result = await downloader.GetMediaUrlAsync("media-bad", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task DownloadMediaAsync_Success_ReturnsBytes()
    {
        var expectedBytes = Encoding.UTF8.GetBytes("fake-image-data");
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "fake-image-data");
        var factory = CreateFactory(handler);
        var downloader = new WhatsAppMediaDownloader(factory, Options.Create(_settings),
            NullLogger<WhatsAppMediaDownloader>.Instance);

        var result = await downloader.DownloadMediaAsync("https://cdn.whatsapp.net/media/123", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task DownloadMediaAsync_Failure_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.NotFound, "not found");
        var factory = CreateFactory(handler);
        var downloader = new WhatsAppMediaDownloader(factory, Options.Create(_settings),
            NullLogger<WhatsAppMediaDownloader>.Instance);

        var result = await downloader.DownloadMediaAsync("https://cdn.whatsapp.net/media/bad", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(WhatsAppMediaDownloader.HttpClientName)
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com") });
        return factory;
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
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
