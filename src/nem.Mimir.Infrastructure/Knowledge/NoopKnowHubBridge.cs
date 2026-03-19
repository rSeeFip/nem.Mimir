namespace nem.Mimir.Infrastructure.Knowledge;

using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Knowledge;

internal sealed class NoopKnowHubBridge(ILogger<NoopKnowHubBridge> logger) : IKnowHubBridge
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<KnowledgeSearchResult>> SearchKnowledgeAsync(
        string query,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        logger.LogDebug("NoopKnowHubBridge: returning empty SearchKnowledge result.");
        return Task.FromResult<IReadOnlyList<KnowledgeSearchResult>>([]);
    }

    public Task<IReadOnlyList<DistilledKnowledge>> DistillKnowledgeAsync(
        string content,
        string source,
        CancellationToken ct = default)
    {
        logger.LogDebug("NoopKnowHubBridge: returning empty DistillKnowledge result.");
        return Task.FromResult<IReadOnlyList<DistilledKnowledge>>([]);
    }

    public Task<GraphQueryResult> QueryGraphAsync(
        string query,
        string tenantId,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        logger.LogDebug("NoopKnowHubBridge: returning fallback QueryGraph result.");
        return Task.FromResult(new GraphQueryResult(
            Answer: string.Empty,
            Mode: "Disabled",
            RelevantEntities: [],
            RelevantCommunities: [],
            ContextChunks: [],
            Score: 0));
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        logger.LogDebug("NoopKnowHubBridge: returning empty embedding.");
        return Task.FromResult(Array.Empty<float>());
    }
}
