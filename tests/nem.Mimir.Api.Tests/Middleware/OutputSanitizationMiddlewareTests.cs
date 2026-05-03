using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Api.Middleware;
using nem.Mimir.Application.Common.Sanitization;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Api.Tests.Middleware;

public sealed class OutputSanitizationMiddlewareTests
{
    private readonly ILogger<OutputSanitizationMiddleware> _logger =
        Substitute.For<ILogger<OutputSanitizationMiddleware>>();

    private readonly ISanitizationService _sanitizationService =
        Substitute.For<ISanitizationService>();

    private OutputSanitizationMiddleware CreateMiddleware(RequestDelegate next, SanitizationOptions? options = null)
    {
        var opts = Options.Create(options ?? new SanitizationOptions());
        return new OutputSanitizationMiddleware(next, _logger, opts);
    }

    private static DefaultHttpContext CreateHttpContext(string method = "GET", string path = "/api/test")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    // ── XSS payload in query params → logged as warning ─────────────────────

    [Fact]
    public async Task InvokeAsync_PostWithSuspiciousQueryParam_LogsWarning()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            new SanitizationOptions { DefaultMode = SanitizationMode.Log });
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?search=<script>alert('xss')</script>");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
        nextCalled.ShouldBeTrue();
    }

    // ── SQL injection pattern in query → logged ─────────────────────────────

    [Fact]
    public async Task InvokeAsync_PutWithSqlInjectionQueryParam_LogsWarning()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask,
            new SanitizationOptions { DefaultMode = SanitizationMode.Log });
        var context = CreateHttpContext("PUT", "/api/conversations/1");
        context.Request.QueryString = new QueryString("?title='; DROP TABLE users; --");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── null/empty response body → passthrough without crash ────────────────

    [Fact]
    public async Task InvokeAsync_NullResponseBody_PassesThroughWithoutCrash()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("GET", "/api/messages");

        await middleware.InvokeAsync(context, _sanitizationService);
    }

    [Fact]
    public async Task InvokeAsync_PostToNonMessageEndpoint_SkipsSuspiciousPatternCheck()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("POST", "/api/health");
        context.Request.QueryString = new QueryString("?query=<script>xss</script>");

        await middleware.InvokeAsync(context, _sanitizationService);

        _sanitizationService.DidNotReceive().ContainsSuspiciousPatterns(Arg.Any<string>());
    }

    // ── GET requests → skipped (only POST/PUT are checked) ──────────────────

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task InvokeAsync_NonPostPutMethod_SkipsSuspiciousPatternCheck(string method)
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext(method, "/api/messages");
        context.Request.QueryString = new QueryString("?q=<script>alert(1)</script>");

        await middleware.InvokeAsync(context, _sanitizationService);

        _sanitizationService.DidNotReceive().ContainsSuspiciousPatterns(Arg.Any<string>());
    }

    // ── Multiple suspicious query params → each logged ──────────────────────

    [Fact]
    public async Task InvokeAsync_MultipleSuspiciousQueryParams_LogsWarningForEach()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask,
            new SanitizationOptions { DefaultMode = SanitizationMode.Log });
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?a=<script>x</script>&b=DROP+TABLE");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        _logger.Received(2).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── Clean query params → no warning ─────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_CleanQueryParams_DoesNotLogWarning()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?page=1&size=10");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(false);

        await middleware.InvokeAsync(context, _sanitizationService);

        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── Log mode: always calls next ──────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_SuspiciousInput_LogMode_StillCallsNextDelegate()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            new SanitizationOptions { DefaultMode = SanitizationMode.Log });
        var context = CreateHttpContext("POST", "/hubs/chat");
        context.Request.QueryString = new QueryString("?msg=<script>alert('xss')</script>");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        nextCalled.ShouldBeTrue();
    }

    // ── Path matching is case-insensitive ───────────────────────────────────

    [Theory]
    [InlineData("/api/Messages")]
    [InlineData("/api/CONVERSATION/1")]
    [InlineData("/Hubs/Chat")]
    public async Task InvokeAsync_CaseInsensitivePath_StillChecksForSuspiciousPatterns(string path)
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("POST", path);
        context.Request.QueryString = new QueryString("?q=test");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(false);

        await middleware.InvokeAsync(context, _sanitizationService);

        _sanitizationService.Received().ContainsSuspiciousPatterns(Arg.Any<string>());
    }

    // ── Null path → no crash ────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_NullRequestPath_DoesNotThrow()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("POST", "");

        await middleware.InvokeAsync(context, _sanitizationService);
    }

    // ── Block mode: returns 400 and does NOT call next ───────────────────────

    [Fact]
    public async Task InvokeAsync_BlockMode_SuspiciousInput_Returns400()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            new SanitizationOptions { DefaultMode = SanitizationMode.Block });
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?q=<script>xss</script>");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        context.Response.StatusCode.ShouldBe(400);
        nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_BlockMode_SuspiciousInput_ResponseContainsProblemDetails()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask,
            new SanitizationOptions { DefaultMode = SanitizationMode.Block });
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?q=<script>xss</script>");
        context.Response.Body = new MemoryStream();

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.ShouldContain("Injection attempt detected");
        (context.Response.ContentType ?? string.Empty).ShouldContain("application/problem+json");
    }

    [Fact]
    public async Task InvokeAsync_BlockMode_CleanInput_CallsNext()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            new SanitizationOptions { DefaultMode = SanitizationMode.Block });
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?q=hello");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(false);

        await middleware.InvokeAsync(context, _sanitizationService);

        nextCalled.ShouldBeTrue();
    }

    // ── Sanitize mode: strips patterns and calls next ────────────────────────

    [Fact]
    public async Task InvokeAsync_SanitizeMode_SuspiciousInput_CallsNext()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            new SanitizationOptions { DefaultMode = SanitizationMode.Sanitize });
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?q=<script>xss</script>");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldNotBe(400);
    }

    [Fact]
    public async Task InvokeAsync_SanitizeMode_SuspiciousInput_StripsDangerousPatterns()
    {
        var capturedQuery = string.Empty;
        var middleware = CreateMiddleware(ctx =>
        {
            capturedQuery = ctx.Request.QueryString.Value ?? string.Empty;
            return Task.CompletedTask;
        }, new SanitizationOptions { DefaultMode = SanitizationMode.Sanitize });
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?q=hello<script>xss</script>world");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        capturedQuery.ShouldNotContain("<script");
        capturedQuery.ShouldContain("hello");
        capturedQuery.ShouldContain("world");
    }

    // ── EnforcementEnabled=false: always passes through regardless of mode ───

    [Theory]
    [InlineData(SanitizationMode.Block)]
    [InlineData(SanitizationMode.Sanitize)]
    [InlineData(SanitizationMode.Log)]
    public async Task InvokeAsync_EnforcementDisabled_AlwaysCallsNext(SanitizationMode mode)
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            new SanitizationOptions { EnforcementEnabled = false, DefaultMode = mode });
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?q=<script>xss</script>");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldNotBe(400);
    }

    [Fact]
    public async Task InvokeAsync_EnforcementDisabled_BlockMode_StillLogs()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask,
            new SanitizationOptions { EnforcementEnabled = false, DefaultMode = SanitizationMode.Block });
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?q=<script>xss</script>");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── Channel override: telegram path uses telegram mode ───────────────────

    [Fact]
    public async Task InvokeAsync_TelegramPath_UsesChannelOverrideMode()
    {
        var nextCalled = false;
        var options = new SanitizationOptions
        {
            DefaultMode = SanitizationMode.Log,
            ChannelOverrides = new Dictionary<string, SanitizationMode>
            {
                ["telegram"] = SanitizationMode.Block
            }
        };
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, options);
        var context = CreateHttpContext("POST", "/api/telegram/messages");
        context.Request.QueryString = new QueryString("?q=<script>xss</script>");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        await middleware.InvokeAsync(context, _sanitizationService);

        context.Response.StatusCode.ShouldBe(400);
        nextCalled.ShouldBeFalse();
    }
}
