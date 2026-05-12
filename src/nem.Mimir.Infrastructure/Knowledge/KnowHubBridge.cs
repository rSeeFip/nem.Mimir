namespace nem.Mimir.Infrastructure.Knowledge;

using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Knowledge;
using nem.KnowHub.Enhancement.Abstractions.Interfaces;
using nem.KnowHub.Enhancement.Distillation.Interfaces;
using nem.KnowHub.Enhancement.GraphRag.Interfaces;

internal sealed class KnowHubBridge(
    IVectorSearchService vectorSearchService,
    IKnowledgeDistillationService knowledgeDistillationService,
    IGraphRagSearcher graphRagSearcher,
    IEmbeddingService embeddingService,
    ILogger<KnowHubBridge> logger) : IKnowHubBridge
{
    private static readonly TimeSpan HealthProbeInterval = TimeSpan.FromMinutes(1);
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".ogg", ".m4a", ".flac"
    };

    private static readonly HashSet<string> CadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dwg", ".dxf", ".ifc", ".step", ".stp", ".iges", ".igs", ".stl"
    };

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".jsx", ".ts", ".tsx", ".py", ".java", ".go", ".rb", ".php", ".cpp", ".c", ".h", ".hpp", ".json", ".xml", ".yml", ".yaml", ".sql"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".txt", ".md", ".rtf"
    };

    private DateTimeOffset _lastHealthProbeAt = DateTimeOffset.MinValue;
    private bool _isAvailable = true;

    public bool IsAvailable => _isAvailable;

    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchKnowledgeAsync(
        string query,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        try
        {
            await ProbeAvailabilityIfNeededAsync(ct);

            var queryEmbedding = await embeddingService.EmbedAsync(query, ct: ct);
            var searchResults = await vectorSearchService.SearchAsync(queryEmbedding.Vector, topK: maxResults, cancellationToken: ct);

            MarkAvailable();

            return searchResults
                .Select(x => new KnowledgeSearchResult(
                    x.ContentId,
                    x.ChunkText,
                    x.Similarity,
                    x.EntityType,
                    x.EntityId,
                    BuildOriginLink(x)))
                .ToList();
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex, "knowledge search");
            return [];
        }
    }

    public async Task<IReadOnlyList<DistilledKnowledge>> DistillKnowledgeAsync(
        string content,
        string source,
        CancellationToken ct = default)
    {
        try
        {
            await ProbeAvailabilityIfNeededAsync(ct);

            var results = await knowledgeDistillationService.DistillAsync(content, source, ct);

            MarkAvailable();

            return results
                .Select(x => new DistilledKnowledge(
                    x.KnowledgeId,
                    x.Outcome.ToString(),
                    x.Score.Composite,
                    x.Type.ToString(),
                    x.Content))
                .ToList();
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex, "knowledge distillation");
            return [];
        }
    }

    public async Task<GraphQueryResult> QueryGraphAsync(
        string query,
        string tenantId,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        try
        {
            await ProbeAvailabilityIfNeededAsync(ct);

            var result = await graphRagSearcher.HybridSearchAsync(query, tenantId, maxResults, ct);

            MarkAvailable();

            return new GraphQueryResult(
                result.Answer,
                result.Mode.ToString(),
                result.RelevantEntities,
                result.RelevantCommunities,
                result.ContextChunks,
                result.Score);
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex, "graph query");

            return new GraphQueryResult(
                Answer: "",
                Mode: "Unavailable",
                RelevantEntities: [],
                RelevantCommunities: [],
                ContextChunks: [],
                Score: 0);
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        try
        {
            await ProbeAvailabilityIfNeededAsync(ct);

            var embedding = await embeddingService.EmbedAsync(text, ct: ct);

            MarkAvailable();

            return embedding.Vector;
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex, "embedding generation");
            return [];
        }
    }

    private async Task ProbeAvailabilityIfNeededAsync(CancellationToken ct)
    {
        if (_isAvailable)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastHealthProbeAt < HealthProbeInterval)
        {
            return;
        }

        _lastHealthProbeAt = now;

        try
        {
            var dimensions = await embeddingService.GetDimensionsAsync(ct: ct);
            if (dimensions > 0)
            {
                _isAvailable = true;
                logger.LogInformation("KnowHub bridge availability recovered.");
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "KnowHub periodic health probe failed.");
        }
    }

    private void MarkUnavailable(Exception ex, string operation)
    {
        _isAvailable = false;
        logger.LogWarning(ex, "KnowHub bridge failed during {Operation}. Falling back to empty result.", operation);
    }

    private void MarkAvailable()
    {
        _isAvailable = true;
    }

    private static SourceOriginLinkDto? BuildOriginLink(VectorSearchResult searchResult)
    {
        if (string.IsNullOrWhiteSpace(searchResult.OriginUrl))
        {
            return null;
        }

        var displayName = ResolveDisplayName(searchResult);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var originType = ResolveOriginType(searchResult, displayName);
        var includeLineNumbers = string.Equals(originType, "Code", StringComparison.Ordinal);

        return new SourceOriginLinkDto(
            searchResult.OriginUrl,
            originType,
            displayName,
            includeLineNumbers ? searchResult.OriginStartLine : null,
            includeLineNumbers ? searchResult.OriginEndLine : null);
    }

    private static string ResolveDisplayName(VectorSearchResult searchResult)
    {
        if (!string.IsNullOrWhiteSpace(searchResult.OriginFileName))
        {
            return searchResult.OriginFileName;
        }

        if (!string.IsNullOrWhiteSpace(searchResult.OriginFilePath))
        {
            return Path.GetFileName(searchResult.OriginFilePath);
        }

        if (Uri.TryCreate(searchResult.OriginUrl, UriKind.Absolute, out var absoluteUri))
        {
            return Path.GetFileName(Uri.UnescapeDataString(absoluteUri.AbsolutePath));
        }

        return string.Empty;
    }

    private static string ResolveOriginType(VectorSearchResult searchResult, string displayName)
    {
        var normalizedOriginKind = NormalizeOriginKind(searchResult.OriginKind);
        if (normalizedOriginKind is not null)
        {
            return normalizedOriginKind;
        }

        var extension = Path.GetExtension(displayName);
        if (CodeExtensions.Contains(extension) || IsCodeContentType(searchResult.OriginContentType))
        {
            return "Code";
        }

        if (CadExtensions.Contains(extension) || IsCadContentType(searchResult.OriginContentType))
        {
            return "Cad";
        }

        if (AudioExtensions.Contains(extension) || IsAudioContentType(searchResult.OriginContentType))
        {
            return "Audio";
        }

        if (DocumentExtensions.Contains(extension) || IsDocumentContentType(searchResult.OriginContentType))
        {
            return "Document";
        }

        return "Media";
    }

    private static string? NormalizeOriginKind(string? originKind) =>
        originKind?.Trim().ToLowerInvariant() switch
        {
            "document" or "doc" => "Document",
            "code" or "source" or "sourcecode" => "Code",
            "cad" or "drawing" => "Cad",
            "audio" => "Audio",
            "media" => "Media",
            _ => null,
        };

    private static bool IsAudioContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) &&
        contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);

    private static bool IsCadContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) &&
        (contentType.Contains("cad", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("dwg", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("dxf", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("ifc", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("step", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("stl", StringComparison.OrdinalIgnoreCase));

    private static bool IsCodeContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) &&
        (contentType.StartsWith("text/x-", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase));

    private static bool IsDocumentContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) &&
        (contentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("application/msword", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("officedocument", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("text/markdown", StringComparison.OrdinalIgnoreCase));
}
