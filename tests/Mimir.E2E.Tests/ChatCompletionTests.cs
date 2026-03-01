using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Mimir.E2E.Tests;

/// <summary>
/// E2E tests for POST /v1/chat/completions SSE streaming endpoint.
/// Uses WireMock to simulate LiteLLM proxy responses.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class ChatCompletionTests
{
    private readonly E2EWebApplicationFactory _factory;

    public ChatCompletionTests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ChatCompletions_Streaming_ReturnsSseChunks()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var request = new
        {
            model = "qwen-2.5-72b",
            messages = new[]
            {
                new { role = "user", content = "Hello" },
            },
            stream = true,
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"),
        };

        // Act
        var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

        // Assert — status and content type
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/event-stream");

        // Read the full SSE body
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();

        // Should contain data: lines and [DONE]
        body.ShouldContain("data: ");
        body.ShouldContain("[DONE]");
    }

    [Fact]
    public async Task ChatCompletions_EmptyMessages_Returns400()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var request = new
        {
            model = "qwen-2.5-72b",
            messages = Array.Empty<object>(),
            stream = true,
        };

        // Act
        var response = await client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("messages must be a non-empty array");
    }

    [Fact]
    public async Task ChatCompletions_NonStreaming_ReturnsJson()
    {
        // Arrange — configure WireMock for non-streaming response
        _factory.WireMock.Reset();


        _factory.WireMock
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    id = "chatcmpl-nonstream",
                    @object = "chat.completion",
                    created = 1234567890,
                    model = "qwen-2.5-72b",
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            message = new { role = "assistant", content = "Hello from Mimir" },
                            finish_reason = "stop",
                        },
                    },
                    usage = new
                    {
                        prompt_tokens = 10,
                        completion_tokens = 5,
                        total_tokens = 15,
                    },
                })));

        // Re-add health endpoint after reset
        _factory.WireMock
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/health")
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"status\":\"healthy\"}"));

        var client = _factory.CreateAuthenticatedClient();
        var request = new
        {
            model = "qwen-2.5-72b",
            messages = new[]
            {
                new { role = "user", content = "Hello" },
            },
            stream = false,
        };

        // Act
        var response = await client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("chatcmpl-");
    }
}
