namespace Mimir.Infrastructure.LiteLlm;

using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;

/// <summary>
/// LiteLLM HTTP client implementing <see cref="ILlmService"/>.
/// Communicates with the LiteLLM proxy using the OpenAI-compatible API.
/// </summary>
internal sealed class LiteLlmClient : ILlmService
{
    internal const string HttpClientName = "LiteLlm";
    private const string ChatCompletionsEndpoint = "/v1/chat/completions";
    private const string ModelsEndpoint = "/v1/models";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LiteLlmOptions _options;
    private readonly ILogger<LiteLlmClient> _logger;

    public LiteLlmClient(
        IHttpClientFactory httpClientFactory,
        IOptions<LiteLlmOptions> options,
        ILogger<LiteLlmClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LlmResponse> SendMessageAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? _options.DefaultModel : model;

        _logger.LogDebug(
            "Sending non-streaming request to {Model} with {MessageCount} messages",
            effectiveModel, messages.Count);

        var request = BuildRequest(effectiveModel, messages, stream: false);
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await httpClient.PostAsJsonAsync(
            ChatCompletionsEndpoint,
            request,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (completion is null)
        {
            throw new InvalidOperationException("LiteLLM returned null response.");
        }

        var content = completion.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        var finishReason = completion.Choices?.FirstOrDefault()?.FinishReason;
        var usage = completion.Usage;

        _logger.LogDebug(
            "Received response from {Model}: {TokenCount} total tokens",
            completion.Model ?? effectiveModel, usage?.TotalTokens ?? 0);

        return new LlmResponse(
            Content: content,
            Model: completion.Model ?? effectiveModel,
            PromptTokens: usage?.PromptTokens ?? 0,
            CompletionTokens: usage?.CompletionTokens ?? 0,
            TotalTokens: usage?.TotalTokens ?? 0,
            FinishReason: finishReason);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmStreamChunk> StreamMessageAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? _options.DefaultModel : model;

        _logger.LogDebug(
            "Sending streaming request to {Model} with {MessageCount} messages",
            effectiveModel, messages.Count);

        var request = BuildRequest(effectiveModel, messages, stream: true);
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsEndpoint)
        {
            Content = JsonContent.Create(request),
        };

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            await foreach (var chunk in SseStreamParser.ParseAsync(stream, cancellationToken).ConfigureAwait(false))
            {
                yield return chunk;
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmModelInfoDto>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await httpClient.GetAsync(ModelsEndpoint, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var modelsResponse = await response.Content.ReadFromJsonAsync<ModelsListResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (modelsResponse?.Data is null)
            {
                _logger.LogWarning("LiteLLM returned null models list");
                return GetHardcodedModels(isAvailable: false);
            }

            var availableModelIds = modelsResponse.Data
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _logger.LogDebug("Retrieved {ModelCount} available models from LiteLLM", availableModelIds.Count);

            return BuildModelInfoList(availableModelIds);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to LiteLLM proxy at {BaseUrl}", _options.BaseUrl);
            return GetHardcodedModels(isAvailable: false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LiteLLM models response");
            return GetHardcodedModels(isAvailable: false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Request to fetch LiteLLM models was cancelled");
            throw;
        }
    }

    private IReadOnlyList<LlmModelInfoDto> BuildModelInfoList(HashSet<string> availableModelIds)
    {
        var models = new List<LlmModelInfoDto>
        {
            new(
                Id: LlmModels.Fast,
                Name: "Phi-4 Mini",
                ContextWindow: 16384,
                RecommendedUse: "Fast responses, simple queries",
                IsAvailable: availableModelIds.Contains(LlmModels.Fast)),

            new(
                Id: LlmModels.Primary,
                Name: "Qwen 2.5 72B",
                ContextWindow: 131072,
                RecommendedUse: "Complex reasoning, detailed analysis",
                IsAvailable: availableModelIds.Contains(LlmModels.Primary)),

            new(
                Id: LlmModels.Coding,
                Name: "Qwen 2.5 Coder 32B",
                ContextWindow: 131072,
                RecommendedUse: "Code generation, technical tasks",
                IsAvailable: availableModelIds.Contains(LlmModels.Coding)),

            new(
                Id: LlmModels.Embedding,
                Name: "Nomic Embed Text",
                ContextWindow: 8192,
                RecommendedUse: "Text embeddings (not for chat)",
                IsAvailable: availableModelIds.Contains(LlmModels.Embedding)),
        };

        return models.AsReadOnly();
    }

    private static IReadOnlyList<LlmModelInfoDto> GetHardcodedModels(bool isAvailable)
    {
        var models = new List<LlmModelInfoDto>
        {
            new(
                Id: LlmModels.Fast,
                Name: "Phi-4 Mini",
                ContextWindow: 16384,
                RecommendedUse: "Fast responses, simple queries",
                IsAvailable: isAvailable),

            new(
                Id: LlmModels.Primary,
                Name: "Qwen 2.5 72B",
                ContextWindow: 131072,
                RecommendedUse: "Complex reasoning, detailed analysis",
                IsAvailable: isAvailable),

            new(
                Id: LlmModels.Coding,
                Name: "Qwen 2.5 Coder 32B",
                ContextWindow: 131072,
                RecommendedUse: "Code generation, technical tasks",
                IsAvailable: isAvailable),

            new(
                Id: LlmModels.Embedding,
                Name: "Nomic Embed Text",
                ContextWindow: 8192,
                RecommendedUse: "Text embeddings (not for chat)",
                IsAvailable: isAvailable),
        };

        return models.AsReadOnly();
    }

    private static ChatCompletionRequest BuildRequest(
        string model,
        IReadOnlyList<LlmMessage> messages,
        bool stream)
    {
        return new ChatCompletionRequest
        {
            Model = model,
            Messages = messages.Select(m => new ChatMessageRequest
            {
                Role = m.Role,
                Content = m.Content,
            }).ToList(),
            Stream = stream,
        };
    }
}
