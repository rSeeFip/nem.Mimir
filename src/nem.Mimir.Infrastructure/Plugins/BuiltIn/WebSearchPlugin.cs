using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Knowledge;
using nem.Mimir.Domain.Plugins;

namespace nem.Mimir.Infrastructure.Plugins.BuiltIn;

/// <summary>
/// Built-in web search plugin that delegates to <see cref="ISearxngClient"/>
/// resolved per-execution via a scope factory. Exposes a <c>web_search(query, max_results)</c>
/// tool for LLM agents, returning structured results with title, URL, and snippet.
/// </summary>
internal sealed class WebSearchPlugin : IPlugin
{
    private const int DefaultMaxResults = 5;
    private const int MaxAllowedResults = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebSearchPlugin> _logger;

    public WebSearchPlugin(IServiceScopeFactory scopeFactory, ILogger<WebSearchPlugin> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string Id => "mimir.builtin.web-search";
    public string Name => "Web Search";
    public string Version => "1.0.0";
    public string Description => "Searches the web for information using the configured search provider. " +
                                  "Parameters: query (required, string), max_results (optional, int, default 5).";

    public async Task<PluginResult> ExecuteAsync(PluginContext context, CancellationToken ct = default)
    {
        if (!context.Parameters.TryGetValue("query", out var queryObj)
            || queryObj is not string query
            || string.IsNullOrWhiteSpace(query))
        {
            return PluginResult.Failure("Required parameter 'query' is missing or empty.");
        }

        var maxResults = DefaultMaxResults;
        if (context.Parameters.TryGetValue("max_results", out var maxObj))
        {
            maxResults = maxObj switch
            {
                int intVal => intVal,
                long longVal => (int)longVal,
                string strVal when int.TryParse(strVal, out var parsed) => parsed,
                _ => DefaultMaxResults,
            };
        }

        maxResults = Math.Clamp(maxResults, 1, MaxAllowedResults);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var searxngClient = scope.ServiceProvider.GetService<ISearxngClient>();

            if (searxngClient is null)
            {
                return PluginResult.Failure(
                    "Web search is not available. The search provider (SearXNG) is not configured.");
            }

            var results = await searxngClient.SearchAsync(query, ct).ConfigureAwait(false);

            if (results.Count == 0)
            {
                return PluginResult.Success(new Dictionary<string, object>
                {
                    ["query"] = query,
                    ["results"] = Array.Empty<object>(),
                    ["result_count"] = 0,
                    ["summary"] = $"No results found for '{query}'.",
                });
            }

            var limitedResults = results.Take(maxResults).ToList();

            var formattedResults = limitedResults.Select((r, i) => new Dictionary<string, object?>
            {
                ["index"] = i + 1,
                ["title"] = r.Title,
                ["url"] = r.Url,
                ["snippet"] = r.Snippet ?? string.Empty,
            }).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Web search results for: \"{query}\"");
            sb.AppendLine($"Found {limitedResults.Count} result(s):");
            sb.AppendLine();

            foreach (var r in limitedResults)
            {
                sb.AppendLine($"- **{r.Title}**");
                sb.AppendLine($"  URL: {r.Url}");
                if (!string.IsNullOrWhiteSpace(r.Snippet))
                {
                    sb.AppendLine($"  {r.Snippet}");
                }

                sb.AppendLine();
            }

            return PluginResult.Success(new Dictionary<string, object>
            {
                ["query"] = query,
                ["results"] = formattedResults,
                ["result_count"] = limitedResults.Count,
                ["summary"] = sb.ToString().TrimEnd(),
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Web search upstream request failed for query '{Query}'", query);
            return PluginResult.Failure(
                $"Web search failed due to an upstream error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Web search timed out for query '{Query}'", query);
            return PluginResult.Failure(
                "Web search request timed out. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during web search for query '{Query}'", query);
            return PluginResult.Failure(
                $"An unexpected error occurred during web search: {ex.Message}");
        }
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
