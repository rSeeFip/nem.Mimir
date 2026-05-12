using System.Net;
using System.Text;
using System.Text.Json;
using nem.Lume.McpTools.Tools;
using NSubstitute;
using Shouldly;

namespace nem.Lume.McpTools.Tests.Tools;

public sealed class LumeCreateProjectToolTests
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
    public async Task CreateProjectAsync_SuccessResponse_ReturnsBody()
    {
        var responseJson = JsonSerializer.Serialize(new { projectId = "p-1", name = "Alpha" });
        var factory = BuildFactory(HttpStatusCode.Created, responseJson);

        var result = await LumeCreateProjectTool.CreateProjectAsync(
            workspaceId: "ws-1",
            name: "Alpha",
            description: null,
            factory,
            CancellationToken.None);

        result.ShouldBe(responseJson);
    }

    [Fact]
    public async Task CreateProjectAsync_WithDescription_SuccessResponse_ReturnsBody()
    {
        var responseJson = JsonSerializer.Serialize(new { projectId = "p-2", name = "Beta", description = "desc" });
        var factory = BuildFactory(HttpStatusCode.Created, responseJson);

        var result = await LumeCreateProjectTool.CreateProjectAsync(
            workspaceId: "ws-1",
            name: "Beta",
            description: "desc",
            factory,
            CancellationToken.None);

        result.ShouldBe(responseJson);
    }

    [Fact]
    public async Task CreateProjectAsync_NonSuccessResponse_ReturnsErrorJson()
    {
        var factory = BuildFactory(HttpStatusCode.Conflict, "conflict");

        var result = await LumeCreateProjectTool.CreateProjectAsync(
            workspaceId: "ws-1",
            name: "Alpha",
            description: null,
            factory,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("409");
    }

    [Fact]
    public async Task CreateProjectAsync_HttpException_ReturnsErrorJson()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lume-api").Returns(new HttpClient(new ThrowingHttpMessageHandler()));

        var result = await LumeCreateProjectTool.CreateProjectAsync(
            workspaceId: "ws-1",
            name: "Alpha",
            description: null,
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
