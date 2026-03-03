namespace Mimir.Application.Agents;

/// <summary>
/// Represents the result of an intent analysis — determines which service domain
/// the user's query should be routed to.
/// </summary>
/// <param name="Category">The intent category (e.g., "knowledge", "assets", "media", "general").</param>
/// <param name="Confidence">The router's confidence in the classification (0.0–1.0).</param>
/// <param name="SuggestedService">The name of the suggested target service (e.g., "KnowHub", "AssetCore").</param>
public sealed record IntentAnalysis(
    string Category,
    double Confidence,
    string SuggestedService);

/// <summary>
/// Represents the result of routing a query to a target service.
/// </summary>
/// <param name="ServiceName">The service that handled the query.</param>
/// <param name="Success">Whether the routing and execution succeeded.</param>
/// <param name="Response">The response content from the target service, if successful.</param>
/// <param name="ErrorMessage">The error message, if routing failed.</param>
public sealed record RoutingResult(
    string ServiceName,
    bool Success,
    string? Response = null,
    string? ErrorMessage = null);

/// <summary>
/// Analyzes user intent and determines which service domain should handle the query.
/// Uses keyword-based heuristic classification to route queries to the appropriate
/// nem.* service (KnowHub, AssetCore, MediaHub, Mimir, etc.).
/// </summary>
/// <remarks>
/// This is a first-pass heuristic router. In future iterations, the LLM itself
/// can be used for more nuanced intent classification.
/// </remarks>
public sealed class QueryRouter
{
    private static readonly Dictionary<string, string[]> ServiceKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["knowledge"] = [
            "document", "search", "find", "lookup", "wiki", "knowledge", "article",
            "reference", "documentation", "know", "information", "research", "learn"
        ],
        ["assets"] = [
            "asset", "file", "upload", "download", "storage", "image", "photo",
            "attachment", "resource", "catalog", "inventory"
        ],
        ["media"] = [
            "video", "audio", "stream", "media", "play", "record", "podcast",
            "music", "transcode", "encode", "thumbnail"
        ],
        ["code"] = [
            "code", "execute", "run", "compile", "script", "program", "function",
            "debug", "test", "build", "deploy"
        ],
        ["general"] = [
            "help", "hello", "hi", "how", "what", "why", "explain", "summarize",
            "think", "reason", "analyze", "compare", "suggest"
        ],
    };

    private static readonly Dictionary<string, string> CategoryToService = new(StringComparer.OrdinalIgnoreCase)
    {
        ["knowledge"] = "KnowHub",
        ["assets"] = "AssetCore",
        ["media"] = "MediaHub",
        ["code"] = "Mimir",
        ["general"] = "Mimir",
    };

    /// <summary>
    /// Analyzes the user query and determines the most likely intent category
    /// and target service for routing.
    /// </summary>
    /// <param name="query">The user's natural language query.</param>
    /// <returns>An <see cref="IntentAnalysis"/> result with category, confidence, and target service.</returns>
    public IntentAnalysis AnalyzeIntent(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalizedQuery = query.ToLowerInvariant();
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (category, keywords) in ServiceKeywords)
        {
            var score = 0;
            foreach (var keyword in keywords)
            {
                if (normalizedQuery.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    score++;
                }
            }

            scores[category] = score;
        }

        var maxScore = scores.Values.Max();
        if (maxScore == 0)
        {
            // No keywords matched — default to general/Mimir
            return new IntentAnalysis("general", 0.5, "Mimir");
        }

        var bestCategory = scores.First(kvp => kvp.Value == maxScore).Key;
        var totalKeywordHits = scores.Values.Sum();

        // Confidence is the fraction of total hits that belong to the top category
        var confidence = totalKeywordHits > 0
            ? Math.Round((double)maxScore / totalKeywordHits, 2)
            : 0.5;

        // Clamp between 0.3 and 1.0 to avoid extreme values
        confidence = Math.Clamp(confidence, 0.3, 1.0);

        var suggestedService = CategoryToService.GetValueOrDefault(bestCategory, "Mimir");

        return new IntentAnalysis(bestCategory, confidence, suggestedService);
    }

    /// <summary>
    /// Gets the service name for a given intent category.
    /// </summary>
    /// <param name="category">The intent category.</param>
    /// <returns>The target service name.</returns>
    public static string GetServiceForCategory(string category)
    {
        return CategoryToService.GetValueOrDefault(category, "Mimir");
    }

    /// <summary>
    /// Returns all known intent categories.
    /// </summary>
    public static IReadOnlyList<string> GetKnownCategories()
    {
        return CategoryToService.Keys.ToList().AsReadOnly();
    }
}
