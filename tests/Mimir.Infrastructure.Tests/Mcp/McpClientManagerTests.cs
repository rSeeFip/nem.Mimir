using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Mimir.Infrastructure.Mcp;
using Shouldly;

namespace Mimir.Infrastructure.Tests.Mcp;

public sealed class McpClientManagerTests
{
    [Fact]
    public async Task ListToolsAsync_UsesCacheWithinTtl()
    {
        var time = new ManualTimeProvider();
        var handler = new StubHttpHandler(
            [
                ResponseJson(HttpStatusCode.OK, """
                { "tools": [ { "name": "search", "description": "Search tool" } ] }
                """)
            ]);

        var sut = CreateSut(time, handler, new McpServerConfiguration
        {
            Name = "knowhub",
            Url = "https://knowhub.example",
            ToolSchemasCacheTtl = TimeSpan.FromMinutes(5),
        });

        var first = await sut.ListToolsAsync();
        var second = await sut.ListToolsAsync();

        first.Count.ShouldBe(1);
        second.Count.ShouldBe(1);
        handler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task ListToolsAsync_RefreshesAfterTtlExpires()
    {
        var time = new ManualTimeProvider();
        var handler = new StubHttpHandler(
            [
                ResponseJson(HttpStatusCode.OK, """
                { "tools": [ { "name": "search", "description": "Search tool" } ] }
                """),
                ResponseJson(HttpStatusCode.OK, """
                { "tools": [ { "name": "search", "description": "Search tool" } ] }
                """)
            ]);

        var sut = CreateSut(time, handler, new McpServerConfiguration
        {
            Name = "knowhub",
            Url = "https://knowhub.example",
            ToolSchemasCacheTtl = TimeSpan.FromMinutes(5),
        });

        await sut.ListToolsAsync();
        time.Advance(TimeSpan.FromMinutes(6));
        await sut.ListToolsAsync();

        handler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task InvokeToolAsync_RoutesByPrefix()
    {
        var time = new ManualTimeProvider();
        var handler = new StubHttpHandler(
            [
                ResponseJson(HttpStatusCode.OK, """
                { "isError": false, "content": "pong" }
                """)
            ]);

        var sut = CreateSut(time, handler, new McpServerConfiguration
        {
            Name = "knowhub",
            Url = "https://knowhub.example",
        });

        var result = await sut.InvokeToolAsync("knowhub.ping", new Dictionary<string, object>());

        result.Success.ShouldBeTrue();
        result.Content.ShouldBe("pong");
        handler.RequestUris.Single().AbsolutePath.ShouldBe("/tools/call");
    }

    [Fact]
    public async Task InvokeToolAsync_RetriesOnTransientFailures()
    {
        var time = new ManualTimeProvider();
        var handler = new StubHttpHandler(
            [
                Response(HttpStatusCode.InternalServerError, "boom"),
                ResponseJson(HttpStatusCode.OK, """
                { "isError": false, "content": "recovered" }
                """)
            ]);

        var sut = CreateSut(time, handler, new McpServerConfiguration
        {
            Name = "knowhub",
            Url = "https://knowhub.example",
        });

        var result = await sut.InvokeToolAsync("knowhub.repair", new Dictionary<string, object>());

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("500");
        handler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task InvokeToolAsync_UsesDiscoveryWhenNoPrefix()
    {
        var time = new ManualTimeProvider();
        var handler = new StubHttpHandler(
            [
                ResponseJson(HttpStatusCode.OK, """
                { "tools": [ { "name": "summarize", "description": "Summarize" } ] }
                """),
                ResponseJson(HttpStatusCode.OK, """
                { "isError": false, "content": "done" }
                """)
            ]);

        var sut = CreateSut(time, handler, new McpServerConfiguration
        {
            Name = "knowhub",
            Url = "https://knowhub.example",
        });

        var result = await sut.InvokeToolAsync("summarize", new Dictionary<string, object>());

        result.Success.ShouldBeTrue();
        result.Content.ShouldBe("done");
        handler.RequestCount.ShouldBe(2);
    }

    [Fact]
    public async Task InvokeToolAsync_RequiresJwtWhenServerRequiresAuth()
    {
        var time = new ManualTimeProvider();
        var handler = new StubHttpHandler([ResponseJson(HttpStatusCode.OK, "{ \"isError\": false, \"content\": \"ok\" }")]);
        var sut = CreateSut(
            time,
            handler,
            new McpServerConfiguration
            {
                Name = "knowhub",
                Url = "https://knowhub.example",
                RequiresAuth = true,
            },
            httpContextAccessor: new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        var result = await sut.InvokeToolAsync("knowhub.any", new Dictionary<string, object>());

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("requires JWT auth");
    }

    [Fact]
    public async Task InvokeToolAsync_AddsAuthorizationHeader_WhenJwtPresent()
    {
        var time = new ManualTimeProvider();
        var handler = new StubHttpHandler([ResponseJson(HttpStatusCode.OK, "{ \"isError\": false, \"content\": \"ok\" }")]);

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";

        var sut = CreateSut(
            time,
            handler,
            new McpServerConfiguration
            {
                Name = "knowhub",
                Url = "https://knowhub.example",
                RequiresAuth = true,
            },
            new HttpContextAccessor { HttpContext = context });

        var result = await sut.InvokeToolAsync("knowhub.any", new Dictionary<string, object>());

        result.Success.ShouldBeTrue();
        handler.AuthorizationHeaders.Single().ShouldBe("Bearer test-token");
    }

    private static McpClientManager CreateSut(
        TimeProvider timeProvider,
        HttpMessageHandler handler,
        McpServerConfiguration configuration,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        var factory = new StubHttpClientFactory(handler);
        return new McpClientManager(
            factory,
            new[] { configuration },
            httpContextAccessor ?? new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            timeProvider,
            NullLogger<McpClientManager>.Instance);
    }

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string content)
        => new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage ResponseJson(HttpStatusCode statusCode, string json)
        => Response(statusCode, json);

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        private readonly HttpClient _client = new(handler, disposeHandler: false);

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpHandler(IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

        public List<Uri> RequestUris { get; } = [];

        public List<string?> AuthorizationHeaders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUris.Add(request.RequestUri!);
            AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No more queued responses.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
