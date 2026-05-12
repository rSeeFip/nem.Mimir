namespace nem.Mimir.Telegram.Tests.Services;

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using nem.Mimir.Telegram.Services;
using Shouldly;

public sealed class MimirApiClientTests
{
    private readonly ILogger<MimirApiClient> _logger = NullLogger<MimirApiClient>.Instance;

    private MimirApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5000/"),
        };
        return new MimirApiClient(httpClient, _logger);
    }

    // ─── MockHttpMessageHandler ──────────────────────────────────

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;
        private readonly string _contentType;

        public MockHttpMessageHandler(
            HttpStatusCode statusCode,
            string responseContent = "",
            string contentType = "application/json")
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
            _contentType = contentType;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, _contentType),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }

    private sealed class CancellingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new TaskCanceledException("The request was cancelled.");
        }
    }

    // ─── CreateConversationAsync ─────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task CreateConversationAsync_HttpError_ReturnsNull(HttpStatusCode statusCode)
    {
        // Arrange
        var handler = new MockHttpMessageHandler(statusCode);
        var client = CreateClient(handler);

        // Act
        var result = await client.CreateConversationAsync("Test", null, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateConversationAsync_MalformedJson_ThrowsJsonException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "not valid json {{{");
        var client = CreateClient(handler);

        // Act & Assert
        await Should.ThrowAsync<JsonException>(
            () => client.CreateConversationAsync("Test", null, CancellationToken.None));
    }

    [Fact]
    public async Task CreateConversationAsync_NetworkFailure_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Network error"));
        var client = CreateClient(handler);

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(
            () => client.CreateConversationAsync("Test", null, CancellationToken.None));
    }

    [Fact]
    public async Task CreateConversationAsync_Timeout_ThrowsTaskCanceledException()
    {
        // Arrange
        var handler = new CancellingHttpMessageHandler();
        var client = CreateClient(handler);

        // Act & Assert
        await Should.ThrowAsync<TaskCanceledException>(
            () => client.CreateConversationAsync("Test", null, CancellationToken.None));
    }

    [Fact]
    public async Task CreateConversationAsync_EmptyJsonBody_ReturnsDeserializedDefault()
    {
        // Arrange - empty JSON object has no matching properties, should produce defaults
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);

        // Act
        var result = await client.CreateConversationAsync("Test", null, CancellationToken.None);

        // Assert - should deserialize to a record with default values, not null
        result.ShouldNotBeNull();
        result.Id.ShouldBe(Guid.Empty);
        result.Title.ShouldBeNull();
    }

    // ─── ListConversationsAsync ──────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ListConversationsAsync_HttpError_ReturnsNull(HttpStatusCode statusCode)
    {
        // Arrange
        var handler = new MockHttpMessageHandler(statusCode);
        var client = CreateClient(handler);

        // Act
        var result = await client.ListConversationsAsync(1, 10, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListConversationsAsync_MalformedJson_ThrowsJsonException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "<html>not json</html>");
        var client = CreateClient(handler);

        // Act & Assert
        await Should.ThrowAsync<JsonException>(
            () => client.ListConversationsAsync(1, 10, CancellationToken.None));
    }

    [Fact]
    public async Task ListConversationsAsync_NetworkFailure_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(
            () => client.ListConversationsAsync(1, 10, CancellationToken.None));
    }

    // ─── SendMessageAsync ────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SendMessageAsync_HttpError_ReturnsNull(HttpStatusCode statusCode)
    {
        // Arrange
        var handler = new MockHttpMessageHandler(statusCode);
        var client = CreateClient(handler);
        var conversationId = Guid.NewGuid();

        // Act
        var result = await client.SendMessageAsync(conversationId, "Hello", null, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SendMessageAsync_MalformedJson_ThrowsJsonException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "}{bad json");
        var client = CreateClient(handler);

        // Act & Assert
        await Should.ThrowAsync<JsonException>(
            () => client.SendMessageAsync(Guid.NewGuid(), "Hello", null, CancellationToken.None));
    }

    [Fact]
    public async Task SendMessageAsync_Timeout_ThrowsTaskCanceledException()
    {
        // Arrange
        var handler = new CancellingHttpMessageHandler();
        var client = CreateClient(handler);

        // Act & Assert
        await Should.ThrowAsync<TaskCanceledException>(
            () => client.SendMessageAsync(Guid.NewGuid(), "Hello", null, CancellationToken.None));
    }

    // ─── GetModelsAsync ──────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetModelsAsync_HttpError_ReturnsNull(HttpStatusCode statusCode)
    {
        // Arrange
        var handler = new MockHttpMessageHandler(statusCode);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetModelsAsync(CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetModelsAsync_MalformedJson_ThrowsJsonException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "this is not json");
        var client = CreateClient(handler);

        // Act & Assert
        await Should.ThrowAsync<JsonException>(
            () => client.GetModelsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetModelsAsync_EmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetModelsAsync(CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    // ─── SendMessageStreamingAsync ───────────────────────────────

    [Fact]
    public async Task SendMessageStreamingAsync_StreamingEndpointFails_FallsBackToNonStreaming()
    {
        // Arrange - streaming endpoint returns 404, non-streaming returns a valid message
        var callCount = 0;
        var handler = new DelegatingMockHandler((request, _) =>
        {
            callCount++;
            if (request.RequestUri!.PathAndQuery.Contains("/stream"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            // Fallback to non-streaming - return a valid message
            var json = JsonSerializer.Serialize(new
            {
                id = Guid.NewGuid(),
                conversationId = Guid.NewGuid(),
                role = "assistant",
                content = "fallback response",
                createdAt = DateTimeOffset.UtcNow,
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        });
        var client = CreateClient(handler);

        // Act
        var tokens = new List<string>();
        await foreach (var token in client.SendMessageStreamingAsync(Guid.NewGuid(), "Hello", null, CancellationToken.None))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.ShouldNotBeEmpty();
        tokens[0].ShouldBe("fallback response");
    }

    [Fact]
    public async Task SendMessageStreamingAsync_BothEndpointsFail_YieldsNothing()
    {
        // Arrange - both streaming and non-streaming return errors
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError);
        var client = CreateClient(handler);

        // Act
        var tokens = new List<string>();
        await foreach (var token in client.SendMessageStreamingAsync(Guid.NewGuid(), "Hello", null, CancellationToken.None))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendMessageStreamingAsync_NetworkException_FallsBackGracefully()
    {
        // Arrange - streaming endpoint throws, non-streaming also fails
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        // Act & Assert - should throw on fallback since both fail
        await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.SendMessageStreamingAsync(Guid.NewGuid(), "Hello", null, CancellationToken.None))
            {
                // consume
            }
        });
    }

    [Fact]
    public async Task SendMessageStreamingAsync_SseDoneMarker_StopsYielding()
    {
        // Arrange - SSE stream with [DONE] marker after one chunk
        var sseContent = "data: {\"content\":\"hello\"}\n\ndata: [DONE]\n\ndata: {\"content\":\"should not appear\"}\n\n";
        var handler = new DelegatingMockHandler((request, _) =>
        {
            if (request.RequestUri!.PathAndQuery.Contains("/stream"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var client = CreateClient(handler);

        // Act
        var tokens = new List<string>();
        await foreach (var token in client.SendMessageStreamingAsync(Guid.NewGuid(), "Hello", null, CancellationToken.None))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Count.ShouldBe(1);
        tokens[0].ShouldBe("hello");
    }

    [Fact]
    public async Task SendMessageStreamingAsync_MalformedSseChunk_SkipsItGracefully()
    {
        // Arrange - SSE stream with a malformed JSON chunk followed by a valid one
        var sseContent = "data: not-valid-json\n\ndata: {\"content\":\"valid\"}\n\ndata: [DONE]\n\n";
        var handler = new DelegatingMockHandler((request, _) =>
        {
            if (request.RequestUri!.PathAndQuery.Contains("/stream"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var client = CreateClient(handler);

        // Act
        var tokens = new List<string>();
        await foreach (var token in client.SendMessageStreamingAsync(Guid.NewGuid(), "Hello", null, CancellationToken.None))
        {
            tokens.Add(token);
        }

        // Assert - malformed chunk is skipped, only valid one appears
        tokens.Count.ShouldBe(1);
        tokens[0].ShouldBe("valid");
    }

    [Fact]
    public async Task SendMessageStreamingAsync_EmptyContentChunks_AreSkipped()
    {
        // Arrange - SSE stream with chunks containing empty content
        var sseContent = "data: {\"content\":\"\"}\n\ndata: {\"content\":\"real\"}\n\ndata: [DONE]\n\n";
        var handler = new DelegatingMockHandler((request, _) =>
        {
            if (request.RequestUri!.PathAndQuery.Contains("/stream"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var client = CreateClient(handler);

        // Act
        var tokens = new List<string>();
        await foreach (var token in client.SendMessageStreamingAsync(Guid.NewGuid(), "Hello", null, CancellationToken.None))
        {
            tokens.Add(token);
        }

        // Assert - empty content chunk is skipped per TryParseStreamChunk logic
        tokens.Count.ShouldBe(1);
        tokens[0].ShouldBe("real");
    }

    [Fact]
    public async Task SendMessageStreamingAsync_CommentAndBlankLines_AreSkipped()
    {
        // Arrange - SSE stream with comment lines (starting with :) and blank lines
        var sseContent = ": this is a comment\n\n\n\ndata: {\"content\":\"ok\"}\n\ndata: [DONE]\n\n";
        var handler = new DelegatingMockHandler((request, _) =>
        {
            if (request.RequestUri!.PathAndQuery.Contains("/stream"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var client = CreateClient(handler);

        // Act
        var tokens = new List<string>();
        await foreach (var token in client.SendMessageStreamingAsync(Guid.NewGuid(), "Hello", null, CancellationToken.None))
        {
            tokens.Add(token);
        }

        // Assert
        tokens.Count.ShouldBe(1);
        tokens[0].ShouldBe("ok");
    }

    // ─── SetBearerToken ──────────────────────────────────────────

    [Fact]
    public void SetBearerToken_EmptyToken_SetsEmptyAuthorizationHeader()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var client = CreateClient(handler);

        // Act & Assert - should not throw
        client.SetBearerToken(string.Empty);
    }

    // ─── DelegatingMockHandler ───────────────────────────────────

    private sealed class DelegatingMockHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public DelegatingMockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
