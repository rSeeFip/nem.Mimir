namespace nem.Mimir.Infrastructure.Tests.LiteLlm;

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Infrastructure.LiteLlm;
using NSubstitute;
using Shouldly;

public sealed class LiteLlmClientTests
{
    private readonly ILogger<LiteLlmClient> _logger;
    private readonly LiteLlmOptions _options;

    public LiteLlmClientTests()
    {
        _logger = Substitute.For<ILogger<LiteLlmClient>>();
        _options = new LiteLlmOptions
        {
            BaseUrl = "http://localhost:4000",
            ApiKey = "test-key",
            TimeoutSeconds = 30,
            DefaultModel = LlmModels.Primary,
        };
    }

    private LiteLlmClient CreateClient(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_options.BaseUrl),
        };
        factory.CreateClient(LiteLlmClient.HttpClientName).Returns(httpClient);

        var options = Options.Create(_options);
        return new LiteLlmClient(factory, options, _logger);
    }

    // ─── SendMessageAsync Tests ──────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_SuccessfulResponse_ReturnsLlmResponse()
    {
        // Arrange
        var responseBody = new
        {
            id = "chatcmpl-1",
            model = "phi-4-mini",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = "Hello there!" },
                    finish_reason = "stop",
                },
            },
            usage = new
            {
                prompt_tokens = 10,
                completion_tokens = 5,
                total_tokens = 15,
            },
        };

        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(responseBody));

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Hello") };

        // Act
        var result = await client.SendMessageAsync("phi-4-mini", messages);

        // Assert
        result.Content.ShouldBe("Hello there!");
        result.Model.ShouldBe("phi-4-mini");
        result.PromptTokens.ShouldBe(10);
        result.CompletionTokens.ShouldBe(5);
        result.TotalTokens.ShouldBe(15);
        result.FinishReason.ShouldBe("stop");
    }

    [Fact]
    public async Task SendMessageAsync_UsesDefaultModel_WhenModelIsEmpty()
    {
        // Arrange
        var responseBody = new
        {
            id = "chatcmpl-1",
            model = "qwen-2.5-72b",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = "Response" },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 5, completion_tokens = 3, total_tokens = 8 },
        };

        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(responseBody));

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Test") };

        // Act
        var result = await client.SendMessageAsync("", messages);

        // Assert
        result.Model.ShouldBe("qwen-2.5-72b");

        // Verify the request was made with the default model
        handler.LastRequestBody!.ShouldContain("qwen-2.5-72b");
    }

    [Fact]
    public async Task SendMessageAsync_HttpError500_ThrowsHttpRequestException()
    {
        // Arrange
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            """{"error": "Internal Server Error"}""");

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Hello") };

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await client.SendMessageAsync("phi-4-mini", messages);
        });
    }

    [Fact]
    public async Task SendMessageAsync_NullResponse_ThrowsInvalidOperationException()
    {
        // Arrange — return valid HTTP but body deserializes to null
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            "null");

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Hello") };

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await client.SendMessageAsync("phi-4-mini", messages);
        });
    }

    [Fact]
    public async Task SendMessageAsync_PassesModelInRequest()
    {
        // Arrange
        var responseBody = new
        {
            id = "1",
            model = "qwen-2.5-coder-32b",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = "Code" },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 },
        };

        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(responseBody));

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Write code") };

        // Act
        await client.SendMessageAsync("qwen-2.5-coder-32b", messages);

        // Assert
        handler.LastRequestBody!.ShouldContain("qwen-2.5-coder-32b");
    }

    [Fact]
    public async Task SendMessageAsync_CorrectEndpoint_IsCalled()
    {
        // Arrange
        var responseBody = new
        {
            id = "1",
            model = "m",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = "ok" },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 },
        };

        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(responseBody));

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Hi") };

        // Act
        await client.SendMessageAsync("m", messages);

        // Assert
        handler.LastRequestUri.ShouldNotBeNull();
        handler.LastRequestUri!.PathAndQuery.ShouldBe("/v1/chat/completions");
    }

    [Fact]
    public async Task SendMessageAsync_SetsStreamFalse_InRequest()
    {
        // Arrange
        var responseBody = new
        {
            id = "1",
            model = "m",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = "ok" },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 },
        };

        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(responseBody));

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Test") };

        // Act
        await client.SendMessageAsync("m", messages);

        // Assert
        handler.LastRequestBody!.ShouldContain("\"stream\":false");
    }

    // ─── StreamMessageAsync Tests ────────────────────────────────

    [Fact]
    public async Task StreamMessageAsync_SuccessfulStream_YieldsChunks()
    {
        // Arrange
        var sseResponse = """
            data: {"id":"1","model":"phi-4-mini","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

            data: {"id":"1","model":"phi-4-mini","choices":[{"index":0,"delta":{"content":" World"},"finish_reason":null}]}

            data: {"id":"1","model":"phi-4-mini","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: [DONE]

            """;

        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            sseResponse,
            "text/event-stream");

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Hi") };

        // Act
        var chunks = new List<LlmStreamChunk>();
        await foreach (var chunk in client.StreamMessageAsync("phi-4-mini", messages))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Count.ShouldBe(3);
        chunks[0].Content.ShouldBe("Hello");
        chunks[1].Content.ShouldBe(" World");
        chunks[2].FinishReason.ShouldBe("stop");
    }

    [Fact]
    public async Task StreamMessageAsync_HttpError_ThrowsHttpRequestException()
    {
        // Arrange
        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable,
            "Service Unavailable");

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Hi") };

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.StreamMessageAsync("phi-4-mini", messages))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task StreamMessageAsync_SetsStreamTrue_InRequest()
    {
        // Arrange
        var sseResponse = """
            data: {"id":"1","model":"m","choices":[{"index":0,"delta":{"content":"ok"},"finish_reason":null}]}

            data: [DONE]

            """;

        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            sseResponse,
            "text/event-stream");

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Test") };

        // Act
        await foreach (var _ in client.StreamMessageAsync("m", messages))
        {
            // consume
        }

        // Assert
        handler.LastRequestBody!.ShouldContain("\"stream\":true");
    }

    [Fact]
    public async Task StreamMessageAsync_EmptyStream_YieldsNothing()
    {
        // Arrange — just [DONE] immediately
        var sseResponse = """
            data: [DONE]

            """;

        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            sseResponse,
            "text/event-stream");

        var client = CreateClient(handler);
        var messages = new List<LlmMessage> { new("user", "Test") };

        // Act
        var chunks = new List<LlmStreamChunk>();
        await foreach (var chunk in client.StreamMessageAsync("m", messages))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendMessageAsync_MessagesAreSerializedCorrectly()
    {
        // Arrange
        var responseBody = new
        {
            id = "1",
            model = "m",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = "ok" },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 },
        };

        using var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(responseBody));

        var client = CreateClient(handler);
        var messages = new List<LlmMessage>
        {
            new("system", "You are helpful"),
            new("user", "Hello"),
        };

        // Act
        await client.SendMessageAsync("m", messages);

        // Assert — request body should contain both messages
        handler.LastRequestBody!.ShouldContain("\"role\":\"system\"");
        handler.LastRequestBody!.ShouldContain("\"content\":\"You are helpful\"");
        handler.LastRequestBody!.ShouldContain("\"role\":\"user\"");
        handler.LastRequestBody!.ShouldContain("\"content\":\"Hello\"");
    }

    // ─── Mock Handler ────────────────────────────────────────────

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;
        private readonly string _contentType;

        public string? LastRequestBody { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public MockHttpMessageHandler(
            HttpStatusCode statusCode,
            string responseContent,
            string contentType = "application/json")
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
            _contentType = contentType;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, _contentType),
            };
        }
    }
}
