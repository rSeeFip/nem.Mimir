using System.Net;
using Shouldly;

namespace nem.Mimir.E2E.Tests.Security;

/// <summary>
/// E2E tests for adversarial prompt injection detection and enforcement.
/// Tests the OutputSanitizationMiddleware behavior across different channels and modes.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class PromptInjectionE2ETests
{
    private readonly E2EWebApplicationFactory _factory;

    public PromptInjectionE2ETests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── Default channel (Sanitize mode) ────────────────────────────────────

    [Fact]
    public async Task PromptInjection_PostToConversationsWithInjectionInQuery_SanitizesAndPasses()
    {
        // Arrange — default channel uses Sanitize mode: strips patterns, passes through
        var client = _factory.CreateAuthenticatedClient();

        // Act — POST to a message path with injection in query param
        // Middleware intercepts POST to paths containing "conversation", sanitizes query, passes to controller
        var response = await client.PostAsync(
            "/api/conversations?message=%3Cscript%3Ealert(1)%3C%2Fscript%3E",
            null);

        // Assert — should NOT be 400 (that would mean Block mode)
        // Sanitize mode strips the pattern and passes through; controller may return 200, 400 (validation), etc.
        response.StatusCode.ShouldNotBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PromptInjection_PostToConversationsWithSqlInjectionInQuery_SanitizesAndPasses()
    {
        // Arrange — SQL injection pattern in query param, default channel = Sanitize
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsync(
            "/api/conversations?q=%27+OR+%271%27%3D%271",
            null);

        // Assert — Sanitize mode: not blocked (not 400 from middleware)
        response.StatusCode.ShouldNotBe(HttpStatusCode.BadRequest);
    }

    // ── Telegram channel (Block mode) ──────────────────────────────────────

    [Fact]
    public async Task PromptInjection_PostToTelegramPathWithInjectionInQuery_Returns400()
    {
        // Arrange — telegram channel uses Block mode
        var client = _factory.CreateAuthenticatedClient();

        // Act — POST to a path containing "telegram" and "conversation" with injection in query
        var response = await client.PostAsync(
            "/api/telegram/conversations?message=%3Cscript%3Ealert(1)%3C%2Fscript%3E",
            null);

        // Assert — Block mode returns 400
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PromptInjection_PostToTelegramPathWithSqlInjection_Returns400()
    {
        // Arrange — SQL injection via telegram channel (Block mode)
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsync(
            "/api/telegram/conversations?q=UNION+SELECT+*+FROM+users",
            null);

        // Assert — Block mode returns 400
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── False positive regression ───────────────────────────────────────────

    [Fact]
    public async Task PromptInjection_BenignMessageWithIgnoreKeyword_PassesThrough()
    {
        // Arrange — benign content with words that sound like injection but aren't patterns
        var client = _factory.CreateAuthenticatedClient();

        // Act — "ignore" and "instructions" are NOT in the dangerous patterns list
        var response = await client.PostAsync(
            "/api/conversations?message=ignore+previous+instructions+please",
            null);

        // Assert — NOT blocked (no dangerous patterns matched)
        response.StatusCode.ShouldNotBe(HttpStatusCode.BadRequest);
    }

    // ── HTTP method bypass ──────────────────────────────────────────────────

    [Fact]
    public async Task PromptInjection_GetRequestWithInjectionInQuery_PassesThrough()
    {
        // Arrange — middleware only intercepts POST/PUT, not GET
        var client = _factory.CreateAuthenticatedClient();

        // Act — GET request with injection in query param
        var response = await client.GetAsync(
            "/api/conversations?message=%3Cscript%3Ealert(1)%3C%2Fscript%3E");

        // Assert — middleware skips GET requests; controller handles normally (200 or other non-400)
        response.StatusCode.ShouldNotBe(HttpStatusCode.BadRequest);
    }

    // ── Non-message path bypass ─────────────────────────────────────────────

    [Fact]
    public async Task PromptInjection_PostToNonMessagePathWithInjection_PassesThrough()
    {
        // Arrange — middleware only intercepts paths containing "message", "conversation", "hubs/chat"
        var client = _factory.CreateAuthenticatedClient();

        // Act — POST to /api/billing (not a message path) with injection in query
        var response = await client.PostAsync(
            "/api/billing?q=%3Cscript%3Ealert(1)%3C%2Fscript%3E",
            null);

        // Assert — middleware skips non-message paths; not blocked by middleware
        response.StatusCode.ShouldNotBe(HttpStatusCode.BadRequest);
    }
}
