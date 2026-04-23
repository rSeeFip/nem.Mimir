using System.Net;
using System.Text;
using System.Text.Json;
using nem.Lume.McpTools.Tools;
using NSubstitute;
using Shouldly;

namespace nem.Lume.McpTools.Tests.Tools;

public sealed class LumeCreateTaskToolTests
{
    private static readonly string ValidWorkspaceId = Guid.NewGuid().ToString();
    private static readonly string ValidBoardId = Guid.NewGuid().ToString();
    private static readonly string ValidColumnId = Guid.NewGuid().ToString();

    private static IHttpClientFactory BuildFactory(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new StubHttpMessageHandler(statusCode, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://lume-api") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lume-api").Returns(httpClient);
        return factory;
    }

    [Fact]
    public async Task CreateTaskAsync_SuccessResponse_ReturnsBody()
    {
        var responseJson = JsonSerializer.Serialize(new { taskId = "t-1", title = "My task" });
        var factory = BuildFactory(HttpStatusCode.OK, responseJson);

        var result = await LumeCreateTaskTool.CreateTaskAsync(
            workspaceId: ValidWorkspaceId,
            boardId: ValidBoardId,
            columnId: ValidColumnId,
            title: "My task",
            description: null,
            factory,
            CancellationToken.None);

        result.ShouldBe(responseJson);
    }

    [Fact]
    public async Task CreateTaskAsync_NonSuccessResponse_ReturnsErrorJson()
    {
        var factory = BuildFactory(HttpStatusCode.BadRequest, "bad request");

        var result = await LumeCreateTaskTool.CreateTaskAsync(
            workspaceId: ValidWorkspaceId,
            boardId: ValidBoardId,
            columnId: ValidColumnId,
            title: "My task",
            description: null,
            factory,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("400");
    }

    [Fact]
    public async Task CreateTaskAsync_HttpException_ReturnsErrorJson()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lume-api").Returns(new HttpClient(new ThrowingHttpMessageHandler()));

        var result = await LumeCreateTaskTool.CreateTaskAsync(
            workspaceId: ValidWorkspaceId,
            boardId: ValidBoardId,
            columnId: ValidColumnId,
            title: "My task",
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
