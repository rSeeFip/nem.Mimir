using System.Net;
using System.Net.Http.Json;
using System.Text;
using Shouldly;

namespace nem.Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests for the <see cref="Controllers.CodeExecutionController"/>.
/// Focuses on authentication enforcement, route constraint validation, and correct error status codes.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class CodeExecutionControllerTests
{
    private readonly HttpClient _client;

    public CodeExecutionControllerTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Authentication: endpoint requires a valid JWT token ──────────────────

    [Fact]
    public async Task Execute_WithoutToken_Returns401()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var payload = new { Language = "python", Code = "print('hello')" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/code-execution", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Execute_WithEmptyCode_WithoutToken_Returns401()
    {
        // Arrange — empty code would be a validation error, but auth comes first
        var conversationId = Guid.NewGuid();
        var payload = new { Language = "python", Code = "" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/code-execution", payload);

        // Assert — auth is checked before validation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Execute_WithNullLanguage_WithoutToken_Returns401()
    {
        // Arrange — null language would be validation error, but auth comes first
        var conversationId = Guid.NewGuid();
        var payload = new { Language = (string?)null, Code = "print('hello')" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/code-execution", payload);

        // Assert — auth is checked before model binding
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Execute_WithInvalidLanguage_WithoutToken_Returns401()
    {
        // Arrange — "ruby" is not in AllowedLanguages, but auth comes first
        var conversationId = Guid.NewGuid();
        var payload = new { Language = "ruby", Code = "puts 'hello'" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/code-execution", payload);

        // Assert — auth precedes FluentValidation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Route constraint validation ─────────────────────────────────────────

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("abc")]
    public async Task Execute_WithInvalidConversationId_Returns404(string invalidId)
    {
        // Route constraint {conversationId:guid} rejects non-GUIDs → 404
        var payload = new { Language = "python", Code = "print('hello')" };

        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{invalidId}/code-execution", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Execute_WithEmptyGuid_WithoutToken_Returns401()
    {
        // Arrange — Guid.Empty is a valid GUID format, route matches → auth check
        var conversationId = Guid.Empty;
        var payload = new { Language = "python", Code = "print('hello')" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/code-execution", payload);

        // Assert — valid GUID in route → auth check → 401
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Route existence ─────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_RouteExists_DoesNotReturn404()
    {
        // Arrange — use a valid GUID to verify route is mapped
        var conversationId = Guid.NewGuid();
        var payload = new { Language = "python", Code = "x = 1" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/code-execution", payload);

        // Assert — should be 401 (not 404), proving route is mapped
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Execute_GetMethod_ReturnsMethodNotAllowedOr401()
    {
        // Arrange — code-execution only supports POST
        var conversationId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync(
            $"/api/conversations/{conversationId}/code-execution");

        // Assert — should be 401 (auth first) or 405 (method not allowed)
        var statusCode = response.StatusCode;
        (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.MethodNotAllowed)
            .ShouldBeTrue($"Expected 401 or 405 but got {statusCode}");
    }

    // ── Edge cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WithEmptyBody_WithoutToken_Returns401()
    {
        // Arrange — empty POST body; auth comes before body parsing
        var conversationId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync(
            $"/api/conversations/{conversationId}/code-execution",
            new StringContent("", Encoding.UTF8, "application/json"));

        // Assert — auth is checked before content negotiation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Execute_WithNullBody_WithoutToken_Returns401()
    {
        // Arrange — no request content at all
        var conversationId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/conversations/{conversationId}/code-execution");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Execute_WithMalformedJson_WithoutToken_Returns401()
    {
        // Arrange — invalid JSON; auth check comes before JSON parsing
        var conversationId = Guid.NewGuid();
        var content = new StringContent("{invalid json}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(
            $"/api/conversations/{conversationId}/code-execution", content);

        // Assert — auth is checked before JSON deserialization
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Execute_WithExcessivelyLargeCode_WithoutToken_Returns401()
    {
        // Arrange — code exceeding the 50KB limit; auth comes first though
        var conversationId = Guid.NewGuid();
        var largeCode = new string('x', 60 * 1024); // 60KB
        var payload = new { Language = "python", Code = largeCode };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/code-execution", payload);

        // Assert — auth check happens before validation
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
