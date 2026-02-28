using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Mimir.Telegram.Services;

/// <summary>
/// Typed HTTP client for communicating with the Mimir API.
/// </summary>
internal sealed class MimirApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MimirApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public MimirApiClient(HttpClient httpClient, ILogger<MimirApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sets the bearer token for subsequent API calls.
    /// </summary>
    public void SetBearerToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    public async Task<ApiConversationDto?> CreateConversationAsync(string title, string? model, CancellationToken ct)
    {
        var request = new { Title = title, SystemPrompt = (string?)null, Model = model };
        var response = await _httpClient.PostAsJsonAsync("api/conversations", request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to create conversation: {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiConversationDto>(JsonOptions, ct);
    }

    /// <summary>
    /// Lists conversations for the authenticated user.
    /// </summary>
    public async Task<ApiPaginatedList<ApiConversationListDto>?> ListConversationsAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync($"api/conversations?pageNumber={pageNumber}&pageSize={pageSize}", ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to list conversations: {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiPaginatedList<ApiConversationListDto>>(JsonOptions, ct);
    }

    /// <summary>
    /// Sends a message to a conversation and returns the assistant's response.
    /// </summary>
    public async Task<ApiMessageDto?> SendMessageAsync(Guid conversationId, string content, string? model, CancellationToken ct)
    {
        var request = new { Content = content, Model = model };
        var response = await _httpClient.PostAsJsonAsync(
            $"api/conversations/{conversationId}/messages", request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to send message to conversation {ConversationId}: {StatusCode}",
                conversationId, response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiMessageDto>(JsonOptions, ct);
    }

    /// <summary>
    /// Sends a message and streams the response tokens via SSE.
    /// Falls back to non-streaming if the streaming endpoint is not available.
    /// </summary>
    public async IAsyncEnumerable<string> SendMessageStreamingAsync(
        Guid conversationId,
        string content,
        string? model,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Try streaming endpoint first
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/conversations/{conversationId}/messages/stream")
        {
            Content = JsonContent.Create(new { Content = content, Model = model }, options: JsonOptions)
        };

        HttpResponseMessage? response = null;
        bool useStreaming = true;

        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Streaming endpoint returned {StatusCode}, falling back to non-streaming", response.StatusCode);
                response.Dispose();
                response = null;
                useStreaming = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Streaming endpoint not available, falling back to non-streaming");
            response?.Dispose();
            response = null;
            useStreaming = false;
        }

        if (!useStreaming)
        {
            var result = await SendMessageAsync(conversationId, content, model, ct);
            if (result is not null)
            {
                yield return result.Content;
            }

            yield break;
        }

        await using var stream = await response!.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (ct.IsCancellationRequested) yield break;

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(':'))
                continue;

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            var data = line["data: ".Length..];

            if (data == "[DONE]")
                yield break;

            var parsed = TryParseStreamChunk(data);
            if (parsed is not null)
            {
                yield return parsed;
            }
        }
    }

    /// <summary>
    /// Attempts to parse an SSE chunk, returning the content or null on failure.
    /// </summary>
    private string? TryParseStreamChunk(string data)
    {
        try
        {
            var chunk = JsonSerializer.Deserialize<ApiStreamChunk>(data, JsonOptions);
            return chunk is { Content.Length: > 0 } ? chunk.Content : null;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse SSE chunk: {Data}", data);
            return null;
        }
    }

    /// <summary>
    /// Retrieves the list of available LLM models.
    /// </summary>
    public async Task<IReadOnlyList<ApiModelInfoDto>?> GetModelsAsync(CancellationToken ct)
    {
        var response = await _httpClient.GetAsync("api/models", ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to get models: {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<List<ApiModelInfoDto>>(JsonOptions, ct);
    }
}

// --- API DTOs (local copies to avoid referencing Application project) ---

internal sealed record ApiConversationDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

internal sealed record ApiConversationListDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Status,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

internal sealed record ApiMessageDto(
    Guid Id,
    Guid ConversationId,
    string Role,
    string Content,
    string? Model,
    int? TokenCount,
    DateTimeOffset CreatedAt);

internal sealed record ApiModelInfoDto(
    string Id,
    string Name,
    int ContextWindow,
    string RecommendedUse,
    bool IsAvailable);

internal sealed record ApiStreamChunk(
    string Content,
    string? Model,
    string? FinishReason);

internal sealed record ApiPaginatedList<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int TotalPages,
    int TotalCount);
