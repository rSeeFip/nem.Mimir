using System.Net.Http.Headers;
using System.Net.Http.Json;
using nem.Mimir.Tui.Models;

namespace nem.Mimir.Tui.Services;

/// <summary>
/// Handles REST API calls for conversation management.
/// </summary>
internal sealed class ConversationApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationService _authService;

    public ConversationApiService(HttpClient httpClient, AuthenticationService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    /// <summary>
    /// Lists conversations for the current user.
    /// </summary>
    public async Task<PaginatedResponse<ConversationListItemDto>?> ListConversationsAsync(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        ConfigureAuth();
        var response = await _httpClient.GetAsync(
            $"api/conversations?pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaginatedResponse<ConversationListItemDto>>(
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    public async Task<ConversationDetailDto?> CreateConversationAsync(
        string title,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        ConfigureAuth();
        var request = new { Title = title, SystemPrompt = (string?)null, Model = model };
        var response = await _httpClient.PostAsJsonAsync("api/conversations", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConversationDetailDto>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets a conversation by its identifier.
    /// </summary>
    public async Task<ConversationDetailDto?> GetConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        ConfigureAuth();
        var response = await _httpClient.GetAsync($"api/conversations/{conversationId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConversationDetailDto>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Archives a conversation.
    /// </summary>
    public async Task ArchiveConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        ConfigureAuth();
        var response = await _httpClient.PostAsync($"api/conversations/{conversationId}/archive", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private void ConfigureAuth()
    {
        if (_authService.AccessToken is not null)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _authService.AccessToken);
        }
    }
}
