namespace nem.Mimir.Infrastructure.Knowledge;

using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Knowledge;
using nem.KnowHub.Abstractions.Interfaces;
using nem.KnowHub.Distillation.Interfaces;
using nem.KnowHub.GraphRag.Interfaces;

internal sealed class KnowHubBridge(
    IVectorSearchService vectorSearchService,
    IKnowledgeDistillationService knowledgeDistillationService,
    IGraphRagSearcher graphRagSearcher,
    IEmbeddingService embeddingService,
    ILogger<KnowHubBridge> logger) : IKnowHubBridge
{
    private static readonly TimeSpan HealthProbeInterval = TimeSpan.FromMinutes(1);

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
                    x.EntityId))
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
}
