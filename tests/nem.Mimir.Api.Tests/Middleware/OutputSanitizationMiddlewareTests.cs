using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using nem.Mimir.Api.Middleware;
using nem.Mimir.Application.Common.Sanitization;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Api.Tests.Middleware;

/// <summary>
/// Tests for <see cref="OutputSanitizationMiddleware"/>, which logs suspicious patterns
/// detected in incoming POST/PUT request query parameters on message-related endpoints.
/// It does NOT rewrite request/response bodies — sanitization happens in the application layer.
/// </summary>
public sealed class OutputSanitizationMiddlewareTests
{
    private readonly ILogger<OutputSanitizationMiddleware> _logger =
        Substitute.For<ILogger<OutputSanitizationMiddleware>>();

    private readonly ISanitizationService _sanitizationService =
        Substitute.For<ISanitizationService>();

    private OutputSanitizationMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new OutputSanitizationMiddleware(next, _logger);
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
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?search=<script>alert('xss')</script>");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        // Act
        await middleware.InvokeAsync(context, _sanitizationService);

        // Assert
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
        nextCalled.ShouldBeTrue(); // Should still call next — logging only, not blocking
    }

    // ── SQL injection pattern in query → logged ─────────────────────────────

    [Fact]
    public async Task InvokeAsync_PutWithSqlInjectionQueryParam_LogsWarning()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("PUT", "/api/conversations/1");
        context.Request.QueryString = new QueryString("?title='; DROP TABLE users; --");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        // Act
        await middleware.InvokeAsync(context, _sanitizationService);

        // Assert
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
        // Arrange — a GET request with no query params and a null-like body scenario
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("GET", "/api/messages");

        // Act & Assert — should not throw
        await middleware.InvokeAsync(context, _sanitizationService);
    }

    [Fact]
    public async Task InvokeAsync_PostToNonMessageEndpoint_SkipsSuspiciousPatternCheck()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("POST", "/api/health");
        context.Request.QueryString = new QueryString("?query=<script>xss</script>");

        // Act
        await middleware.InvokeAsync(context, _sanitizationService);

        // Assert — should NOT have checked since path doesn't contain message/conversation/hubs/chat
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
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext(method, "/api/messages");
        context.Request.QueryString = new QueryString("?q=<script>alert(1)</script>");

        // Act
        await middleware.InvokeAsync(context, _sanitizationService);

        // Assert
        _sanitizationService.DidNotReceive().ContainsSuspiciousPatterns(Arg.Any<string>());
    }

    // ── Multiple suspicious query params → each logged ──────────────────────

    [Fact]
    public async Task InvokeAsync_MultipleSuspiciousQueryParams_LogsWarningForEach()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?a=<script>x</script>&b=DROP+TABLE");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        // Act
        await middleware.InvokeAsync(context, _sanitizationService);

        // Assert — should log for each suspicious param
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
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("POST", "/api/messages");
        context.Request.QueryString = new QueryString("?page=1&size=10");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(false);

        // Act
        await middleware.InvokeAsync(context, _sanitizationService);

        // Assert
        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── Next delegate is always called (middleware never blocks) ─────────────

    [Fact]
    public async Task InvokeAsync_SuspiciousInput_StillCallsNextDelegate()
    {
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext("POST", "/hubs/chat");
        context.Request.QueryString = new QueryString("?msg=<script>alert('xss')</script>");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(true);

        // Act
        await middleware.InvokeAsync(context, _sanitizationService);

        // Assert — middleware is logging-only; it must always call next
        nextCalled.ShouldBeTrue();
    }

    // ── Path matching is case-insensitive ───────────────────────────────────

    [Theory]
    [InlineData("/api/Messages")]
    [InlineData("/api/CONVERSATION/1")]
    [InlineData("/Hubs/Chat")]
    public async Task InvokeAsync_CaseInsensitivePath_StillChecksForSuspiciousPatterns(string path)
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("POST", path);
        context.Request.QueryString = new QueryString("?q=test");

        _sanitizationService.ContainsSuspiciousPatterns(Arg.Any<string>()).Returns(false);

        // Act
        await middleware.InvokeAsync(context, _sanitizationService);

        // Assert — should have checked since path contains message/conversation/hubs/chat
        _sanitizationService.Received().ContainsSuspiciousPatterns(Arg.Any<string>());
    }

    // ── Null path → no crash ────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_NullRequestPath_DoesNotThrow()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext("POST", "");

        // Act & Assert — should not throw even with empty path
        await middleware.InvokeAsync(context, _sanitizationService);
    }
}
