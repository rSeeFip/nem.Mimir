using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Mimir.E2E.Tests;

/// <summary>
/// E2E tests for conversation CRUD operations via /api/conversations.
/// Uses real PostgreSQL for persistence verification.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class ConversationTests
{
    private readonly E2EWebApplicationFactory _factory;

    public ConversationTests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateConversation_WithValidToken_Returns201()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var client = _factory.CreateAuthenticatedClient(userId);
        var request = new
        {
            title = "E2E Test Conversation",
            systemPrompt = "You are a helpful assistant.",
            model = "qwen-2.5-72b",
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/conversations", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.TryGetProperty("id", out _).ShouldBeTrue();
        root.TryGetProperty("title", out var title).ShouldBeTrue();
        title.GetString().ShouldBe("E2E Test Conversation");
    }

    [Fact]
    public async Task GetConversations_WithValidToken_Returns200()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var client = _factory.CreateAuthenticatedClient(userId);

        // Create a conversation first
        var createRequest = new
        {
            title = "List Test Conversation",
            systemPrompt = (string?)null,
            model = "qwen-2.5-72b",
        };
        await client.PostAsJsonAsync("/api/conversations", createRequest);

        // Act
        var response = await client.GetAsync("/api/conversations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetConversationById_AfterCreate_ReturnsConversation()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var client = _factory.CreateAuthenticatedClient(userId);

        var createRequest = new
        {
            title = "GetById Test Conversation",
            systemPrompt = "Test prompt",
            model = "qwen-2.5-72b",
        };
        var createResponse = await client.PostAsJsonAsync("/api/conversations", createRequest);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createBody = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createBody);
        var conversationId = createDoc.RootElement.GetProperty("id").GetString();
        conversationId.ShouldNotBeNullOrWhiteSpace();

        // Act
        var response = await client.GetAsync($"/api/conversations/{conversationId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("title").GetString().ShouldBe("GetById Test Conversation");
    }

    [Fact]
    public async Task CreateConversation_WithoutToken_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            title = "Unauthorized Conversation",
            systemPrompt = (string?)null,
            model = "qwen-2.5-72b",
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/conversations", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
