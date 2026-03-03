using System.Net;
using System.Text;
using Shouldly;

namespace Mimir.Api.IntegrationTests;

/// <summary>
/// Negative integration tests covering error paths, boundary conditions,
/// invalid inputs, and failure modes across multiple API controllers.
/// Focuses on scenarios NOT covered by existing per-controller tests:
/// wrong HTTP methods, invalid content types, malformed routes, oversized payloads.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ApiIntegrationNegativeTests
{
    private readonly HttpClient _client;

    public ApiIntegrationNegativeTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Completely nonexistent routes ────────────────────────────────────────

    [Theory]
    [InlineData("/api/nonexistent")]
    [InlineData("/api/conversations/actions/invalid")]
    [InlineData("/v2/chat/completions")]
    [InlineData("/api/")]
    public async Task NonexistentRoute_Returns401Or404(string url)
    {
        // Act
        var response = await _client.GetAsync(url);

        // Assert — should be 401 (auth middleware) or 404 (no matching route)
        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.NotFound)
            .ShouldBeTrue($"Expected 401 or 404 for {url} but got {statusCode}");
    }

    // ── Wrong HTTP methods on known routes ──────────────────────────────────

    [Fact]
    public async Task Conversations_DeleteOnListRoute_Returns401OrMethodNotAllowed()
    {
        // DELETE /api/conversations (list route) — only GET and POST are valid
        var response = await _client.DeleteAsync("/api/conversations");

        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.MethodNotAllowed)
            .ShouldBeTrue($"Expected 401 or 405 but got {statusCode}");
    }

    [Fact]
    public async Task Conversations_PatchMethod_Returns401OrMethodNotAllowed()
    {
        // PATCH is not supported on any conversation endpoint
        var conversationId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/conversations/{conversationId}");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);

        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.MethodNotAllowed)
            .ShouldBeTrue($"Expected 401 or 405 but got {statusCode}");
    }

    [Fact]
    public async Task SystemPrompts_DeleteMethod_Returns401OrMethodNotAllowed()
    {
        // DELETE is not a supported method on /api/system-prompts
        var response = await _client.DeleteAsync("/api/system-prompts");

        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized
         || statusCode == HttpStatusCode.MethodNotAllowed
         || statusCode == HttpStatusCode.NotFound)
            .ShouldBeTrue($"Expected 401, 404, or 405 but got {statusCode}");
    }

    // ── Invalid content types ───────────────────────────────────────────────

    [Fact]
    public async Task Conversations_Create_WithXmlContentType_Returns401()
    {
        // Arrange — send XML content type to a JSON-only endpoint
        var content = new StringContent("<xml/>", Encoding.UTF8, "application/xml");

        // Act
        var response = await _client.PostAsync("/api/conversations", content);

        // Assert — auth is checked before content negotiation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChatCompletions_WithPlainTextContentType_Returns401()
    {
        // Arrange — send plain text to a JSON endpoint
        var content = new StringContent("not json at all", Encoding.UTF8, "text/plain");

        // Act
        var response = await _client.PostAsync("/v1/chat/completions", content);

        // Assert — auth is checked before content negotiation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Malformed GUIDs in various routes ────────────────────────────────────

    [Theory]
    [InlineData("/api/conversations/not-a-guid")]
    [InlineData("/api/conversations/12345")]
    [InlineData("/api/conversations/null")]
    [InlineData("/api/admin/users/not-a-guid")]
    public async Task RouteWithInvalidGuid_Returns404(string url)
    {
        // Route constraints {id:guid} reject non-GUIDs → 404
        var response = await _client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Oversized request bodies ────────────────────────────────────────────

    [Fact]
    public async Task Conversations_Create_WithOversizedPayload_Returns401()
    {
        // Arrange — very large title string (100KB+)
        var largeTitle = new string('A', 100 * 1024);
        var json = $$"""{"title":"{{largeTitle}}","systemPrompt":null,"model":null}""";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/conversations", content);

        // Assert — auth check happens before body size validation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Empty and whitespace route segments ──────────────────────────────────

    [Fact]
    public async Task Conversations_ArchiveWithEmptyId_Returns401Or404()
    {
        // POST /api/conversations//archive — empty GUID segment
        var response = await _client.PostAsync("/api/conversations//archive", null);

        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.NotFound)
            .ShouldBeTrue($"Expected 401 or 404 but got {statusCode}");
    }

    [Fact]
    public async Task Messages_WithSpaceInConversationId_Returns404()
    {
        // Spaces in GUID position → route constraint rejects → 404
        var response = await _client.GetAsync("/api/conversations/some%20space/messages");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Multiple invalid parameters at once ─────────────────────────────────

    [Fact]
    public async Task AdminAudit_WithInvalidQueryParams_Returns401()
    {
        // Arrange — query params with invalid values
        var response = await _client.GetAsync(
            "/api/admin/audit?pageNumber=-1&pageSize=0&userId=not-a-guid");

        // Assert — auth check comes first, invalid params would cause 400 after auth
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── HEAD method on various endpoints ────────────────────────────────────

    [Theory]
    [InlineData("/api/conversations")]
    [InlineData("/v1/models")]
    [InlineData("/api/admin/users")]
    public async Task HeadMethod_OnGetEndpoints_Returns401(string url)
    {
        // HEAD should behave like GET for auth purposes
        var request = new HttpRequestMessage(HttpMethod.Head, url);

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── OPTIONS method (CORS preflight) ─────────────────────────────────────

    [Fact]
    public async Task OptionsMethod_OnApiEndpoint_DoesNotReturn500()
    {
        // CORS preflight should not cause server errors
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/conversations");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        // Should not be a server error regardless of CORS config
        ((int)response.StatusCode).ShouldBeLessThan(500);
    }

    // ── Double slashes and path traversal ───────────────────────────────────

    [Theory]
    [InlineData("/api//conversations")]
    [InlineData("/api/conversations/../admin/users")]
    public async Task MalformedPaths_DoNotReturn200(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK,
            $"Malformed path {url} should not return 200 OK");
    }

    // ── Health endpoint edge cases ──────────────────────────────────────────

    [Fact]
    public async Task Health_PostMethod_Returns404OrMethodNotAllowed()
    {
        // /health only supports GET
        var response = await _client.PostAsync("/health", null);

        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.NotFound || statusCode == HttpStatusCode.MethodNotAllowed)
            .ShouldBeTrue($"Expected 404 or 405 for POST /health but got {statusCode}");
    }

    // ── Request with empty JSON object to endpoints expecting specific shape ─

    [Fact]
    public async Task Messages_SendWithEmptyJson_Returns401()
    {
        // Arrange — empty JSON body to message endpoint
        var conversationId = Guid.NewGuid();
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(
            $"/api/conversations/{conversationId}/messages", content);

        // Assert — auth check before validation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminUpdateRole_WithEmptyJson_Returns401()
    {
        // Arrange — empty JSON body for role update
        var userId = Guid.NewGuid();
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync($"/api/admin/users/{userId}/role", content);

        // Assert — auth check before model binding
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Concurrent request simulation ───────────────────────────────────────

    [Fact]
    public async Task MultipleUnauthenticatedRequests_AllReturn401()
    {
        // Fire 10 concurrent unauthenticated requests — all should get 401
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync("/api/conversations"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }
}
