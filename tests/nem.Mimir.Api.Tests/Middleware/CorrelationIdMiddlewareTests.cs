using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using nem.Mimir.Api.Middleware;
using Shouldly;

namespace nem.Mimir.Api.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    private CorrelationIdMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new CorrelationIdMiddleware(next);
    }

    /// <summary>
    /// A custom <see cref="IHttpResponseFeature"/> that stores OnStarting callbacks
    /// and invokes them when <see cref="FireOnStarting"/> is called.
    /// DefaultHttpContext does not fire OnStarting callbacks via StartAsync, so we
    /// need this to properly test middleware that uses Response.OnStarting().
    /// </summary>
    private sealed class TestHttpResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStartingCallbacks = [];

        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state)
        {
            _onStartingCallbacks.Add((callback, state));
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
            // Not needed for these tests
        }

        public async Task FireOnStarting()
        {
            HasStarted = true;
            // Fire in reverse order (same as ASP.NET Core)
            for (var i = _onStartingCallbacks.Count - 1; i >= 0; i--)
            {
                var (callback, state) = _onStartingCallbacks[i];
                await callback(state);
            }
        }
    }

    private static (DefaultHttpContext Context, TestHttpResponseFeature ResponseFeature) CreateHttpContext()
    {
        var responseFeature = new TestHttpResponseFeature();
        var featureCollection = new FeatureCollection();
        featureCollection.Set<IHttpResponseFeature>(responseFeature);
        featureCollection.Set<IHttpRequestFeature>(new HttpRequestFeature());
        var context = new DefaultHttpContext(featureCollection);
        return (context, responseFeature);
    }

    // ── No correlation ID header → generates valid UUID ─────────────────────

    [Fact]
    public async Task InvokeAsync_NoCorrelationIdHeader_GeneratesNewId()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var (context, responseFeature) = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStarting();

        // Assert
        responseFeature.Headers.TryGetValue("X-Correlation-Id", out var value).ShouldBeTrue();
        var correlationId = value.ToString();
        correlationId.ShouldNotBeNullOrWhiteSpace();

        // Should be a valid GUID (format "N" = no dashes)
        Guid.TryParse(correlationId, out _).ShouldBeTrue();
    }

    // ── Valid X-Correlation-Id header → preserved in response ───────────────

    [Fact]
    public async Task InvokeAsync_ValidCorrelationIdHeader_PreservedInResponse()
    {
        // Arrange
        const string existingId = "my-custom-correlation-id-12345";
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var (context, responseFeature) = CreateHttpContext();
        context.Request.Headers["X-Correlation-Id"] = existingId;

        // Act
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStarting();

        // Assert
        responseFeature.Headers["X-Correlation-Id"].ToString().ShouldBe(existingId);
    }

    // ── X-Request-Id header fallback ────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_XRequestIdHeader_UsedAsFallback()
    {
        // Arrange
        const string requestId = "request-id-fallback-789";
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var (context, responseFeature) = CreateHttpContext();
        context.Request.Headers["X-Request-Id"] = requestId;

        // Act
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStarting();

        // Assert
        responseFeature.Headers["X-Correlation-Id"].ToString().ShouldBe(requestId);
    }

    // ── Invalid/malformed correlation ID (whitespace) → regenerated ─────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task InvokeAsync_WhitespaceCorrelationId_RegeneratesNewId(string malformedId)
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var (context, responseFeature) = CreateHttpContext();
        context.Request.Headers["X-Correlation-Id"] = malformedId;

        // Act
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStarting();

        // Assert — should have generated a new valid GUID, not used the blank one
        var correlationId = responseFeature.Headers["X-Correlation-Id"].ToString();
        correlationId.ShouldNotBeNullOrWhiteSpace();
        correlationId.ShouldNotBe(malformedId);
        Guid.TryParse(correlationId, out _).ShouldBeTrue();
    }

    // ── Correlation ID present in response headers ──────────────────────────

    [Fact]
    public async Task InvokeAsync_Always_SetsCorrelationIdInResponseHeaders()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var (context, responseFeature) = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStarting();

        // Assert
        responseFeature.Headers.ContainsKey("X-Correlation-Id").ShouldBeTrue();
    }

    // ── X-Correlation-Id takes precedence over X-Request-Id ─────────────────

    [Fact]
    public async Task InvokeAsync_BothHeadersPresent_CorrelationIdTakesPrecedence()
    {
        // Arrange
        const string correlationId = "correlation-wins";
        const string requestId = "request-loses";
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var (context, responseFeature) = CreateHttpContext();
        context.Request.Headers["X-Correlation-Id"] = correlationId;
        context.Request.Headers["X-Request-Id"] = requestId;

        // Act
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStarting();

        // Assert
        responseFeature.Headers["X-Correlation-Id"].ToString().ShouldBe(correlationId);
    }

    // ── Next delegate is always called ──────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_Always_CallsNextDelegate()
    {
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var (context, _) = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.ShouldBeTrue();
    }

    // ── Each request gets a unique correlation ID ───────────────────────────

    [Fact]
    public async Task InvokeAsync_MultipleRequests_EachGetsUniqueCorrelationId()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        var (context1, responseFeature1) = CreateHttpContext();
        var (context2, responseFeature2) = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context1);
        await responseFeature1.FireOnStarting();

        await middleware.InvokeAsync(context2);
        await responseFeature2.FireOnStarting();

        // Assert
        var id1 = responseFeature1.Headers["X-Correlation-Id"].ToString();
        var id2 = responseFeature2.Headers["X-Correlation-Id"].ToString();
        id1.ShouldNotBe(id2);
    }

    // ── Non-parseable correlation ID is still preserved (not just GUIDs) ────

    [Fact]
    public async Task InvokeAsync_NonGuidCorrelationId_StillPreserved()
    {
        // Arrange — middleware doesn't validate format, any non-whitespace value is kept
        const string nonGuidId = "custom-trace-ABC-123";
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var (context, responseFeature) = CreateHttpContext();
        context.Request.Headers["X-Correlation-Id"] = nonGuidId;

        // Act
        await middleware.InvokeAsync(context);
        await responseFeature.FireOnStarting();

        // Assert
        responseFeature.Headers["X-Correlation-Id"].ToString().ShouldBe(nonGuidId);
    }
}
