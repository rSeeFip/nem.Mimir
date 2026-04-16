using System.Net;
using System.Text;
using System.Text.Json;
using NSubstitute;
using Shouldly;
using nem.Lume.McpTools.Tools;

namespace nem.Lume.McpTools.Tests.Tools;

public sealed class LumeSummarizeDocumentToolTests
{
    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lume-api").Returns(client);
        return factory;
    }

    [Fact]
    public async Task SummarizeDocumentAsync_HappyPath_ReturnsSummary()
    {
        var responseJson = """{"summary":"This document covers Lume onboarding.","documentId":"doc-42"}""";
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        var result = await LumeSummarizeDocumentTool.SummarizeDocumentAsync(
            "doc-42",
            CreateFactory(handler),
            CancellationToken.None);

        result.ShouldContain("Lume onboarding");
        result.ShouldContain("doc-42");
        handler.LastRequestUri!.AbsolutePath.ShouldBe("/api/lume/documents/doc-42/summarize");
    }

    [Fact]
    public async Task SummarizeDocumentAsync_ApiError_ReturnsErrorJson()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Document not found", Encoding.UTF8, "text/plain"),
        });

        var result = await LumeSummarizeDocumentTool.SummarizeDocumentAsync(
            "missing-doc",
            CreateFactory(handler),
            CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString()!.ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("404");
    }

    [Fact]
    public async Task SummarizeDocumentAsync_NetworkException_ReturnsErrorJson()
    {
        var handler = new ThrowingHandler(new HttpRequestException("host unreachable"));

        var result = await LumeSummarizeDocumentTool.SummarizeDocumentAsync(
            "doc-1",
            CreateFactory(handler),
            CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString()!.ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("host unreachable");
    }

    private sealed class FakeHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
