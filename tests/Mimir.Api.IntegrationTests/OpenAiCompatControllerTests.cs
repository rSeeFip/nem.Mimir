using System.Net;
using System.Net.Http.Json;
using System.Text;
using Shouldly;

namespace Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests for the <see cref="Controllers.OpenAiCompatController"/>.
/// Focuses on authentication enforcement, route validation, and correct error status codes.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class OpenAiCompatControllerTests
{
    private readonly HttpClient _client;

    public OpenAiCompatControllerTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Authentication: all endpoints require a valid JWT token ──────────────

    [Theory]
    [InlineData("POST", "/v1/chat/completions")]
    [InlineData("GET", "/v1/models")]
    public async Task AllEndpoints_WithoutToken_Return401(string method, string url)
    {
        // Arrange
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        if (method is "POST")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Chat Completions: authentication ────────────────────────────────────

    [Fact]
    public async Task ChatCompletions_WithoutToken_Returns401()
    {
        // Arrange
        var payload = new
        {
            model = "test-model",
            messages = new[] { new { role = "user", content = "Hello" } },
            stream = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChatCompletions_WithEmptyMessages_WithoutToken_Returns401()
    {
        // Arrange — empty messages array would be 400, but auth comes first
        var payload = new
        {
            model = "test-model",
            messages = Array.Empty<object>(),
            stream = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);

        // Assert — auth is checked before message validation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChatCompletions_WithInvalidModel_WithoutToken_Returns401()
    {
        // Arrange — invalid model name; auth comes first
        var payload = new
        {
            model = "non-existent-model-xyz-999",
            messages = new[] { new { role = "user", content = "Hello" } },
            stream = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);

        // Assert — auth is checked before model validation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChatCompletions_Streaming_WithoutToken_Returns401()
    {
        // Arrange — streaming request; auth comes first
        var payload = new
        {
            model = "test-model",
            messages = new[] { new { role = "user", content = "Hello" } },
            stream = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);

        // Assert — auth is checked before streaming starts
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Models listing: authentication ──────────────────────────────────────

    [Fact]
    public async Task ListModels_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/v1/models");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Route existence ─────────────────────────────────────────────────────

    [Fact]
    public async Task ChatCompletions_RouteExists_DoesNotReturn404()
    {
        // Arrange
        var payload = new
        {
            model = "test-model",
            messages = new[] { new { role = "user", content = "Hello" } }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);

        // Assert — should be 401, not 404
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListModels_RouteExists_DoesNotReturn404()
    {
        // Act
        var response = await _client.GetAsync("/v1/models");

        // Assert — should be 401, not 404
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    // ── Edge cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatCompletions_WithEmptyBody_WithoutToken_Returns401()
    {
        // Act — empty body; auth comes first
        var response = await _client.PostAsync(
            "/v1/chat/completions",
            new StringContent("", Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChatCompletions_WithMalformedJson_WithoutToken_Returns401()
    {
        // Arrange — invalid JSON; auth is checked before JSON parsing
        var content = new StringContent("{invalid}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/v1/chat/completions", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChatCompletions_WithNullContent_WithoutToken_Returns401()
    {
        // Arrange — no request body
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChatCompletions_GetMethod_Returns401OrMethodNotAllowed()
    {
        // Arrange — chat/completions only supports POST
        var response = await _client.GetAsync("/v1/chat/completions");

        // Assert — should be 401 (auth first) or 405 (method not allowed)
        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.MethodNotAllowed)
            .ShouldBeTrue($"Expected 401 or 405 but got {statusCode}");
    }

    [Fact]
    public async Task NonexistentEndpoint_UnderV1_Returns401OrNotFound()
    {
        // Act — unknown endpoint under /v1
        var response = await _client.GetAsync("/v1/nonexistent");

        // Assert — should be 401 (auth) or 404 (not found)
        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.NotFound)
            .ShouldBeTrue($"Expected 401 or 404 but got {statusCode}");
    }

    [Fact]
    public async Task ChatCompletions_WithNullMessagesField_WithoutToken_Returns401()
    {
        // Arrange — JSON with explicit null messages; auth comes first
        var json = """{"model": "test-model", "messages": null, "stream": false}""";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/v1/chat/completions", content);

        // Assert — auth is checked before body deserialization
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListModels_PostMethod_Returns401OrMethodNotAllowed()
    {
        // Arrange — models only supports GET
        var response = await _client.PostAsync("/v1/models", null);

        // Assert — should be 401 or 405
        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.MethodNotAllowed)
            .ShouldBeTrue($"Expected 401 or 405 but got {statusCode}");
    }
}
