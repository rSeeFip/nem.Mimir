using System.Net;
using System.Text;
using System.Text.Json;
using NSubstitute;
using Shouldly;
using nem.Lume.McpTools.Tools;

namespace nem.Lume.McpTools.Tests.Tools;

public sealed class LumeCreateArticleToolTests
{
    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lume-api").Returns(client);
        return factory;
    }

    [Fact]
    public async Task CreateArticleAsync_HappyPath_ReturnsCreatedArticle()
    {
        var responseJson = """{"id":"article-999","title":"Getting Started","spaceId":"space-1"}""";
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        var result = await LumeCreateArticleTool.CreateArticleAsync(
            "space-1",
            "Getting Started",
            "# Introduction\nWelcome to Lume.",
            CreateFactory(handler),
            CancellationToken.None);

        result.ShouldContain("article-999");
        result.ShouldContain("Getting Started");
        handler.LastRequestUri!.AbsolutePath.ShouldBe("/api/lume/kb/spaces/space-1/articles");
    }

    [Fact]
    public async Task CreateArticleAsync_SendsTitleAndContent()
    {
        var responseJson = """{"id":"article-1"}""";
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        await LumeCreateArticleTool.CreateArticleAsync(
            "space-x",
            "My Article",
            "Some content here",
            CreateFactory(handler),
            CancellationToken.None);

        var bodyJson = await handler.LastRequestContent!.ReadAsStringAsync();
        bodyJson.ShouldContain("My Article");
        bodyJson.ShouldContain("Some content here");
    }

    [Fact]
    public async Task CreateArticleAsync_ApiError_ReturnsErrorJson()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Space not found", Encoding.UTF8, "text/plain"),
        });

        var result = await LumeCreateArticleTool.CreateArticleAsync(
            "missing-space",
            "Title",
            "Content",
            CreateFactory(handler),
            CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString()!.ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("404");
    }

    [Fact]
    public async Task CreateArticleAsync_NetworkException_ReturnsErrorJson()
    {
        var handler = new ThrowingHandler(new HttpRequestException("connection reset"));

        var result = await LumeCreateArticleTool.CreateArticleAsync(
            "space-1",
            "Title",
            "Content",
            CreateFactory(handler),
            CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString()!.ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("connection reset");
    }

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
