using System.Net;
using System.Text;
using System.Text.Json;
using nem.Lume.McpTools.Tools;
using NSubstitute;
using Shouldly;

namespace nem.Lume.McpTools.Tests.Tools;

public sealed class LumeMoveTaskToolTests
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
    public async Task MoveTaskAsync_SuccessResponse_ReturnsBody()
    {
        var responseJson = JsonSerializer.Serialize(new { taskId = "t-1", columnId = "col-2" });
        var factory = BuildFactory(HttpStatusCode.OK, responseJson);

        var result = await LumeMoveTaskTool.MoveTaskAsync(
            taskId: "t-1",
            targetColumnId: "col-2",
            position: null,
            factory,
            CancellationToken.None);

        result.ShouldBe(responseJson);
    }

    [Fact]
    public async Task MoveTaskAsync_WithPosition_SuccessResponse_ReturnsBody()
    {
        var responseJson = JsonSerializer.Serialize(new { taskId = "t-1", columnId = "col-2", position = 0 });
        var factory = BuildFactory(HttpStatusCode.OK, responseJson);

        var result = await LumeMoveTaskTool.MoveTaskAsync(
            taskId: "t-1",
            targetColumnId: "col-2",
            position: 0,
            factory,
            CancellationToken.None);

        result.ShouldBe(responseJson);
    }

    [Fact]
    public async Task MoveTaskAsync_NonSuccessResponse_ReturnsErrorJson()
    {
        var factory = BuildFactory(HttpStatusCode.NotFound, "not found");

        var result = await LumeMoveTaskTool.MoveTaskAsync(
            taskId: "t-missing",
            targetColumnId: "col-2",
            position: null,
            factory,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("404");
    }

    [Fact]
    public async Task MoveTaskAsync_HttpException_ReturnsErrorJson()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lume-api").Returns(new HttpClient(new ThrowingHttpMessageHandler()));

        var result = await LumeMoveTaskTool.MoveTaskAsync(
            taskId: "t-1",
            targetColumnId: "col-2",
            position: null,
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
