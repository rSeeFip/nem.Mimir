using System.Net;
using System.Text;
using System.Text.Json;
using nem.Lume.McpTools.Tools;
using NSubstitute;
using Shouldly;

namespace nem.Lume.McpTools.Tests.Tools;

public sealed class LumeListProjectsToolTests
{
    private static IHttpClientFactory BuildFactory(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new StubHttpMessageHandler(statusCode, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://lume-api") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lume-api").Returns(httpClient);
        return factory;
    }

    [Fact]
    public async Task ListProjectsAsync_SuccessResponse_ReturnsBody()
    {
        var responseJson = JsonSerializer.Serialize(new { projects = new[] { new { id = "p-1", name = "Alpha" } } });
        var factory = BuildFactory(HttpStatusCode.OK, responseJson);

        var result = await LumeListProjectsTool.ListProjectsAsync(
            workspaceId: "ws-1",
            factory,
            CancellationToken.None);

        result.ShouldBe(responseJson);
    }

    [Fact]
    public async Task ListProjectsAsync_NonSuccessResponse_ReturnsErrorJson()
    {
        var factory = BuildFactory(HttpStatusCode.Forbidden, "forbidden");

        var result = await LumeListProjectsTool.ListProjectsAsync(
            workspaceId: "ws-1",
            factory,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("403");
    }

    [Fact]
    public async Task ListProjectsAsync_HttpException_ReturnsErrorJson()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lume-api").Returns(new HttpClient(new ThrowingHttpMessageHandler()));

        var result = await LumeListProjectsTool.ListProjectsAsync(
            workspaceId: "ws-1",
            factory,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("error");
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Network failure");
    }
}
