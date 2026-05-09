using System.Net;
using System.Text;
using System.Text.Json;
using NSubstitute;
using Shouldly;
using nem.Lume.McpTools.Tools;

namespace nem.Lume.McpTools.Tests.Tools;

public sealed class LumeSearchDocumentsToolTests
{
    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lume-api").Returns(client);
        return factory;
    }

    [Fact]
    public async Task SearchDocumentsAsync_HappyPath_ReturnsResults()
    {
        var responseJson = """{"results":[{"excerpt":"Meeting notes","documentId":"doc-1","score":0.95}],"elapsedMs":12.5}""";
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        var result = await LumeSearchDocumentsTool.SearchDocumentsAsync(
            "find meeting notes",
            "ws-abc",
            5,
            CreateFactory(handler),
            CancellationToken.None);

        result.ShouldContain("Meeting notes");
        result.ShouldContain("doc-1");
        handler.LastRequestUri!.AbsolutePath.ShouldBe("/api/lume/search/rag");
    }

    [Fact]
    public async Task SearchDocumentsAsync_NullTopK_DefaultsToTen()
    {
        var responseJson = """{"results":[],"elapsedMs":1.0}""";
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        await LumeSearchDocumentsTool.SearchDocumentsAsync(
            "query",
            "ws-1",
            null,
            CreateFactory(handler),
            CancellationToken.None);

        var bodyJson = await handler.LastRequestContent!.ReadAsStringAsync();
        bodyJson.ShouldContain("\"topK\":10");
    }

    [Fact]
    public async Task SearchDocumentsAsync_ApiError_ReturnsErrorJson()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("Forbidden", Encoding.UTF8, "text/plain"),
        });

        var result = await LumeSearchDocumentsAsync(handler);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("403");
    }

    [Fact]
    public async Task SearchDocumentsAsync_NetworkException_ReturnsErrorJson()
    {
        var handler = new ThrowingHandler(new HttpRequestException("timeout"));

        var result = await LumeSearchDocumentsAsync(handler);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("timeout");
    }

    private static Task<string> LumeSearchDocumentsAsync(HttpMessageHandler handler)
        => LumeSearchDocumentsTool.SearchDocumentsAsync(
            "query",
            "ws-1",
            10,
            CreateFactory(handler),
            CancellationToken.None);

    private sealed class FakeHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public HttpContent? LastRequestContent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestContent = request.Content;
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
