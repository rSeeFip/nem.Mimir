namespace Mimir.Infrastructure.LiteLlm;

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Contracts.AspNetCore.Classification;
using nem.Contracts.Classification;

public sealed class LiteLlmClassificationInterceptor : DelegatingHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClassificationOptions _options;
    private readonly ILogger<LiteLlmClassificationInterceptor> _logger;

    public LiteLlmClassificationInterceptor(
        IHttpClientFactory httpClientFactory,
        IOptions<ClassificationOptions> options,
        ILogger<LiteLlmClassificationInterceptor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var level = await ClassifyRequestAsync(request, cancellationToken).ConfigureAwait(false);
        request.Options.Set(ClassificationGatingHandler.ClassificationLevelOptionKey, level);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ClassificationLevel> ClassifyRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var requestBody = await ReadAndRestoreContentAsync(request, cancellationToken).ConfigureAwait(false);
            var promptText = ExtractPromptText(requestBody);

            if (string.IsNullOrWhiteSpace(promptText))
            {
                return ClassificationConstants.DefaultLevel;
            }

            var conversationId = ExtractConversationId(requestBody);
            var payload = new ClassificationRequest(promptText, "llm-prompt", conversationId);

            using var client = _httpClientFactory.CreateClient("ClassificationApi");
            var classifyUri = BuildClassifyUri(client.BaseAddress, _options.ClassificationApiBaseUrl);
            using var response = await client.PostAsJsonAsync(classifyUri, payload, JsonOptions, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Classification API returned non-success status {StatusCode}. Falling back to default classification level.",
                    (int)response.StatusCode);
                return ClassificationConstants.DefaultLevel;
            }

            var result = await response.Content.ReadFromJsonAsync<ClassificationResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
            return ParseClassificationLevel(result?.Level);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to classify LiteLLM request. Falling back to default classification level.");
            return ClassificationConstants.DefaultLevel;
        }
    }

    private static async Task<string?> ReadAndRestoreContentAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
        {
            return null;
        }

        var originalContent = request.Content;
        var contentString = await originalContent.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var mediaType = originalContent.Headers.ContentType?.MediaType ?? "application/json";
        var charset = originalContent.Headers.ContentType?.CharSet;
        var encoding = string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);

        var replacementContent = new StringContent(contentString, encoding, mediaType);
        foreach (var header in originalContent.Headers)
        {
            if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            replacementContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        request.Content = replacementContent;
        return contentString;
    }

    private static string ExtractPromptText(string? requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(requestBody);
        if (!document.RootElement.TryGetProperty("messages", out var messages) ||
            messages.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        string? fallback = null;
        for (var i = messages.GetArrayLength() - 1; i >= 0; i--)
        {
            var message = messages[i];
            var content = ExtractContent(message);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            fallback = content;
            if (message.TryGetProperty("role", out var role) &&
                string.Equals(role.GetString(), "user", StringComparison.OrdinalIgnoreCase))
            {
                return content;
            }
        }

        return fallback ?? string.Empty;
    }

    private static Uri BuildClassifyUri(Uri? baseAddress, string configuredBaseAddress)
    {
        if (baseAddress is not null)
        {
            return new Uri(baseAddress, "/api/v1/classify");
        }

        return new Uri(new Uri(configuredBaseAddress), "/api/v1/classify");
    }

    private static string ExtractConversationId(string? requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return Guid.NewGuid().ToString("N");
        }

        try
        {
            using var document = JsonDocument.Parse(requestBody);
            var root = document.RootElement;

            if (TryGetString(root, "conversationId", out var conversationId) ||
                TryGetString(root, "conversation_id", out conversationId))
            {
                return conversationId;
            }
        }
        catch (JsonException)
        {
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var parsed = property.GetString();
        if (string.IsNullOrWhiteSpace(parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static string? ExtractContent(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content))
        {
            return null;
        }

        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString(),
            JsonValueKind.Array => string.Join(' ', content.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("text", out var text)
                    ? text.GetString()
                    : item.GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text))),
            _ => null,
        };
    }

    private static ClassificationLevel ParseClassificationLevel(object? level)
    {
        if (level is null)
        {
            return ClassificationConstants.DefaultLevel;
        }

        if (level is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numeric) &&
                Enum.IsDefined(typeof(ClassificationLevel), numeric))
            {
                return (ClassificationLevel)numeric;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (Enum.TryParse<ClassificationLevel>(text, true, out var parsed))
                {
                    return parsed;
                }
            }

            return ClassificationConstants.DefaultLevel;
        }

        if (level is int value && Enum.IsDefined(typeof(ClassificationLevel), value))
        {
            return (ClassificationLevel)value;
        }

        if (Enum.TryParse<ClassificationLevel>(level.ToString(), true, out var fallback))
        {
            return fallback;
        }

        return ClassificationConstants.DefaultLevel;
    }

    private sealed record ClassificationRequest(string Text, string EntityType, string EntityId);

    private sealed record ClassificationResponse(object? Level);
}
