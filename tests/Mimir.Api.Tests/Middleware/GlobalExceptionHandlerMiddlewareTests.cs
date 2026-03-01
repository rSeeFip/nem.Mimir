using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mimir.Api.Middleware;
using Mimir.Application.Common.Exceptions;
using NSubstitute;
using Shouldly;

namespace Mimir.Api.Tests.Middleware;

public sealed class GlobalExceptionHandlerMiddlewareTests
{
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger =
        Substitute.For<ILogger<GlobalExceptionHandlerMiddleware>>();

    private GlobalExceptionHandlerMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new GlobalExceptionHandlerMiddleware(next, _logger);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonDocument> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    // ── Unhandled Exception → 500 ProblemDetails ────────────────────────────

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500WithProblemDetails()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("boom"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        context.Response.ContentType!.ShouldContain("application/json");

        using var doc = await ReadResponseBody(context);
        doc.RootElement.GetProperty("status").GetInt32().ShouldBe(500);
        doc.RootElement.GetProperty("title").GetString().ShouldBe("Internal Server Error");
    }

    // ── OperationCanceledException → 499 ────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_OperationCanceledException_Returns499()
    {
        // Arrange
        var context = CreateHttpContext();
        var cts = new CancellationTokenSource();
        context.RequestAborted = cts.Token;
        await cts.CancelAsync();

        var middleware = CreateMiddleware(_ => throw new OperationCanceledException());

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(499);
    }

    [Fact]
    public async Task InvokeAsync_OperationCanceledException_DoesNotWriteResponseBody()
    {
        // Arrange
        var context = CreateHttpContext();
        var cts = new CancellationTokenSource();
        context.RequestAborted = cts.Token;
        await cts.CancelAsync();

        var middleware = CreateMiddleware(_ => throw new OperationCanceledException());

        // Act
        await middleware.InvokeAsync(context);

        // Assert — body should remain empty (no ProblemDetails written)
        context.Response.Body.Length.ShouldBe(0);
    }

    // ── Inner exception info NOT leaked ─────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UnhandledException_DoesNotLeakExceptionMessage()
    {
        // Arrange
        const string secretMessage = "Connection string: Server=prod;Password=s3cret";
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException(secretMessage));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.ShouldNotContain(secretMessage);
        body.ShouldNotContain("s3cret");
        body.ShouldNotContain("Connection string");
    }

    [Fact]
    public async Task InvokeAsync_NestedInnerException_DoesNotLeakInnerExceptionDetails()
    {
        // Arrange
        const string innerSecret = "SQL error: invalid column 'password_hash'";
        var inner = new Exception(innerSecret);
        var outer = new InvalidOperationException("Outer error", inner);
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw outer);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.ShouldNotContain(innerSecret);
        body.ShouldNotContain("password_hash");
    }

    // ── Stack trace NOT included ────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UnhandledException_DoesNotIncludeStackTrace()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("test error"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.ShouldNotContain("at Mimir");
        body.ShouldNotContain("stackTrace");
        body.ShouldNotContain("StackTrace");
    }

    // ── ValidationException → 400 ──────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns400()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Name", new[] { "Name is required" } }
        };
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new ValidationException(errors));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        using var doc = await ReadResponseBody(context);
        doc.RootElement.GetProperty("title").GetString().ShouldBe("Validation Error");
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_IncludesValidationErrors()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Email", new[] { "Email is invalid" } }
        };
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new ValidationException(errors));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        using var doc = await ReadResponseBody(context);
        doc.RootElement.TryGetProperty("errors", out _).ShouldBeTrue();
    }

    // ── NotFoundException → 404 ─────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_NotFoundException_Returns404()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new NotFoundException("User", "abc-123"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        using var doc = await ReadResponseBody(context);
        doc.RootElement.GetProperty("title").GetString().ShouldBe("Not Found");
    }

    // ── ForbiddenAccessException → 403 ──────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_ForbiddenAccessException_Returns403()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new ForbiddenAccessException());

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        using var doc = await ReadResponseBody(context);
        doc.RootElement.GetProperty("title").GetString().ShouldBe("Forbidden");
    }

    [Fact]
    public async Task InvokeAsync_ForbiddenAccessException_DoesNotLeakCustomMessage()
    {
        // Arrange
        const string sensitiveReason = "User lacks AdminPolicy on resource /api/admin/secrets";
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new ForbiddenAccessException(sensitiveReason));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.ShouldNotContain(sensitiveReason);
    }

    // ── ConflictException → 409 ─────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_ConflictException_Returns409()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new ConflictException());

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        using var doc = await ReadResponseBody(context);
        doc.RootElement.GetProperty("title").GetString().ShouldBe("Conflict");
    }

    // ── ProblemDetails always has traceId and instance ───────────────────────

    [Fact]
    public async Task InvokeAsync_AnyException_ResponseIncludesTraceId()
    {
        // Arrange
        var context = CreateHttpContext();
        context.TraceIdentifier = "test-trace-id-12345";
        var middleware = CreateMiddleware(_ => throw new Exception("fail"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        using var doc = await ReadResponseBody(context);
        doc.RootElement.TryGetProperty("traceId", out var traceId).ShouldBeTrue();
        traceId.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_AnyException_ResponseIncludesInstancePath()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Path = "/api/conversations/42";
        var middleware = CreateMiddleware(_ => throw new Exception("fail"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        using var doc = await ReadResponseBody(context);
        doc.RootElement.GetProperty("instance").GetString().ShouldBe("/api/conversations/42");
    }

    // ── 500 errors are logged as Error, others as Warning ───────────────────

    [Fact]
    public async Task InvokeAsync_UnhandledException_LogsAsError()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("unexpected"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_LogsAsWarningNotError()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new NotFoundException("Entity", "123"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        _logger.DidNotReceive().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
