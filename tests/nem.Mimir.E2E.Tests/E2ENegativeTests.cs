using System.Net;
using System.Net.Http.Json;
using System.Text;
using Shouldly;

namespace nem.Mimir.E2E.Tests;

/// <summary>
/// E2E negative tests covering error paths, boundary conditions,
/// invalid inputs, and failure modes across the full application stack.
/// Uses real PostgreSQL, RabbitMQ, and WireMock infrastructure.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class E2ENegativeTests
{
    private readonly E2EWebApplicationFactory _factory;

    public E2ENegativeTests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Malformed JWT tokens ────────────────────────────────────────────────

    [Fact]
    public async Task Request_WithMalformedBearerToken_Returns401()
    {
        // Arrange — token that is not valid JWT at all
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-jwt-token");

        // Act
        var response = await client.GetAsync("/api/conversations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithEmptyBearerToken_Returns401()
    {
        // Arrange — Bearer header with empty value
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "");

        // Act
        var response = await client.GetAsync("/api/conversations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithWrongSigningKey_Returns401()
    {
        // Arrange — generate a token with a different signing key
        var wrongKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("Wrong-Signing-Key-That-Is-Also-Long-Enough-For-HS256!!!!!"));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            wrongKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: Helpers.JwtTokenHelper.TestIssuer,
            audience: Helpers.JwtTokenHelper.TestAudience,
            claims: new[]
            {
                new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenString);

        // Act
        var response = await client.GetAsync("/api/conversations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithWrongIssuer_Returns401()
    {
        // Arrange — valid signing key but wrong issuer
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(Helpers.JwtTokenHelper.TestSigningKey));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "https://wrong-issuer.example.com",
            audience: Helpers.JwtTokenHelper.TestAudience,
            claims: new[]
            {
                new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenString);

        // Act
        var response = await client.GetAsync("/api/conversations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithWrongAudience_Returns401()
    {
        // Arrange — valid signing key and issuer but wrong audience
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(Helpers.JwtTokenHelper.TestSigningKey));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: Helpers.JwtTokenHelper.TestIssuer,
            audience: "wrong-audience",
            claims: new[]
            {
                new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenString);

        // Act
        var response = await client.GetAsync("/api/conversations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Nonexistent resources with valid auth ───────────────────────────────

    [Fact]
    public async Task GetConversation_NonexistentId_Returns404Or500()
    {
        // Arrange — authenticated but requesting a conversation that doesn't exist
        var client = _factory.CreateAuthenticatedClient();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/conversations/{nonExistentId}");

        // Assert — should return 404 (not found) or could be handled differently
        var statusCode = (int)response.StatusCode;
        (statusCode == 404 || statusCode >= 400)
            .ShouldBeTrue($"Expected 4xx for nonexistent resource but got {statusCode}");
    }

    [Fact]
    public async Task DeleteConversation_NonexistentId_ReturnsError()
    {
        // Arrange — authenticated, deleting conversation that doesn't exist
        var client = _factory.CreateAuthenticatedClient();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/api/conversations/{nonExistentId}");

        // Assert — should be a client error (4xx), not success
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Deleting nonexistent conversation should return error, got {statusCode}");
    }

    // ── Invalid payloads with valid auth ─────────────────────────────────────

    [Fact]
    public async Task CreateConversation_WithEmptyTitle_ReturnsError()
    {
        // Arrange — authenticated but with empty title (should fail validation)
        var client = _factory.CreateAuthenticatedClient();
        var request = new { title = "", systemPrompt = (string?)null, model = "qwen-2.5-72b" };

        // Act
        var response = await client.PostAsJsonAsync("/api/conversations", request);

        // Assert — should fail validation (400) or other error, not 2xx
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Empty title should fail validation, got {statusCode}");
    }

    [Fact]
    public async Task CreateConversation_WithMalformedJson_ReturnsError()
    {
        // Arrange — authenticated but malformed JSON body
        var client = _factory.CreateAuthenticatedClient();
        var content = new StringContent("{invalid json!!}", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/conversations", content);

        // Assert — should be 400 (bad request) for malformed JSON
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Malformed JSON should return client error, got {statusCode}");
    }

    [Fact]
    public async Task CreateConversation_WithNullBody_ReturnsError()
    {
        // Arrange — authenticated but no body at all
        var client = _factory.CreateAuthenticatedClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/conversations");

        // Act
        var response = await client.SendAsync(request);

        // Assert — should be 400 or 415 (unsupported media type)
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Null body should return client error, got {statusCode}");
    }

    // ── Invalid route parameters with valid auth ────────────────────────────

    [Fact]
    public async Task GetConversation_InvalidGuidFormat_Returns404()
    {
        // Arrange — authenticated but invalid GUID in route
        var client = _factory.CreateAuthenticatedClient();

        // Act — route constraint {id:guid} rejects non-GUIDs
        var response = await client.GetAsync("/api/conversations/not-a-guid");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Sending messages to nonexistent conversations ───────────────────────

    [Fact]
    public async Task SendMessage_ToNonexistentConversation_ReturnsError()
    {
        // Arrange — authenticated, sending to conversation that doesn't exist
        var client = _factory.CreateAuthenticatedClient();
        var nonExistentId = Guid.NewGuid();
        var request = new { content = "Hello", model = "qwen-2.5-72b" };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/conversations/{nonExistentId}/messages", request);

        // Assert — should be 404 or other error, not success
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Message to nonexistent conversation should fail, got {statusCode}");
    }

    // ── Cross-user isolation ────────────────────────────────────────────────

    [Fact]
    public async Task GetConversations_DifferentUser_ShouldNotSeeOtherUsersData()
    {
        // Arrange — create conversation as user A
        var userAId = Guid.NewGuid().ToString();
        var userBId = Guid.NewGuid().ToString();

        var clientA = _factory.CreateAuthenticatedClient(userAId);
        var createRequest = new
        {
            title = "User A Private Conversation",
            systemPrompt = (string?)null,
            model = "qwen-2.5-72b",
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/conversations", createRequest);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act — user B tries to list conversations
        var clientB = _factory.CreateAuthenticatedClient(userBId);
        var response = await clientB.GetAsync("/api/conversations");

        // Assert — user B should not see user A's conversations
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("User A Private Conversation");
    }

    // ── Chat completion with invalid payload ────────────────────────────────

    [Fact]
    public async Task ChatCompletion_WithEmptyMessages_ReturnsError()
    {
        // Arrange — authenticated, valid format but empty messages array
        var client = _factory.CreateAuthenticatedClient();
        var request = new
        {
            model = "qwen-2.5-72b",
            messages = Array.Empty<object>(),
            stream = false,
        };

        // Act
        var response = await client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert — empty messages should fail validation
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400).ShouldBeTrue(
            $"Empty messages should fail validation, got {statusCode}");
    }
}
