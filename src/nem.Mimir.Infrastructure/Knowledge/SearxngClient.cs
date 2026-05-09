namespace nem.Mimir.Infrastructure.Knowledge;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Knowledge;

internal sealed class SearxngClient(
    HttpClient httpClient,
    IOptions<SearxngOptions> options,
    ILogger<SearxngClient> logger) : ISearxngClient
{
    public async Task<IReadOnlyList<WebSearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var maxResults = Math.Clamp(options.Value.MaxResults, 1, 50);
        var url = $"/search?q={Uri.EscapeDataString(query)}&format=json";

        using var response = await httpClient.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("SearXNG search failed with status {StatusCode}", response.StatusCode);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<WebSearchResultDto>(maxResults);
        foreach (var item in resultsElement.EnumerateArray())
        {
            var title = TryGetString(item, "title");
            var urlValue = TryGetString(item, "url");

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(urlValue))
            {
                continue;
            }

            var snippet = TryGetString(item, "content");
            var source = TryGetString(item, "engine");
            var publishedAt = ParseDate(TryGetString(item, "publishedDate"));

            results.Add(new WebSearchResultDto(title, urlValue, snippet, source, publishedAt));
            if (results.Count >= maxResults)
            {
                break;
            }
        }

        return results;
    }

    private static string? TryGetString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.GetString();
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;
    }
}
