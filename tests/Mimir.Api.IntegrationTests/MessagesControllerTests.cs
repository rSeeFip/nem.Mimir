using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests for the MessagesController endpoints.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class MessagesControllerTests
{
    private readonly HttpClient _client;

    public MessagesControllerTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendMessage_WithoutToken_Returns401()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var request = new { Content = "Hello", Model = (string?)null };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMessages_WithoutToken_Returns401()
    {
        // Arrange
        var conversationId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync(
            $"/api/conversations/{conversationId}/messages");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMessages_WithoutToken_WithPagination_Returns401()
    {
        // Arrange
        var conversationId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync(
            $"/api/conversations/{conversationId}/messages?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    public async Task SendMessage_WithInvalidConversationId_Returns404Or400(string invalidId)
    {
        // Act — invalid GUID in route should result in 404 (route constraint) or 400
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{invalidId}/messages",
            new { Content = "Hello" });

        // Assert — route constraint {conversationId:guid} rejects non-GUIDs with 404
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
