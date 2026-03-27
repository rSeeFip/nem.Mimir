using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Knowledge;
using nem.Contracts.Memory;

namespace nem.Mimir.Application.Services.Memory;

public sealed class SemanticMemoryService : ISemanticMemory
{
    private const string DefaultUserId = "default";

    private readonly ISemanticFactRepository _repository;
    private readonly IKnowHubBridge _knowHubBridge;
    private readonly SemanticMemoryOptions _options;
    private readonly ILogger<SemanticMemoryService> _logger;

    public SemanticMemoryService(
        ISemanticFactRepository repository,
        IKnowHubBridge knowHubBridge,
        SemanticMemoryOptions options,
        ILogger<SemanticMemoryService> logger)
    {
        _repository = repository;
        _knowHubBridge = knowHubBridge;
        _options = options;
        _logger = logger;
    }

    public async Task StoreFactAsync(KnowledgeFact fact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ValidateFact(fact);

        if (fact.Confidence < _options.MinConfidence)
        {
            _logger.LogDebug("Semantic fact {FactId} skipped because confidence {Confidence} < {MinConfidence}.", fact.Id, fact.Confidence, _options.MinConfidence);
            return;
        }

        var existing = await _repository.GetByFactIdAsync(fact.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            var currentCount = await _repository.CountByUserAsync(DefaultUserId, cancellationToken).ConfigureAwait(false);
            if (currentCount >= _options.MaxFactsPerUser)
            {
                throw new InvalidOperationException($"Maximum facts per user ({_options.MaxFactsPerUser}) reached.");
            }
        }

        var allFacts = await _repository.GetByUserAsync(DefaultUserId, cancellationToken).ConfigureAwait(false);
        var related = FindRelatedFactIds(fact, allFacts, fact.Id);

        float[]? embedding = null;
        if (_knowHubBridge.IsAvailable)
        {
            try
            {
                embedding = await _knowHubBridge.GenerateEmbeddingAsync(BuildFactText(fact), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding generation failed for semantic fact {FactId}; persisting without embedding.", fact.Id);
            }
        }

        var entity = new SemanticFactEntity
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            FactId = fact.Id,
            Subject = fact.Subject,
            Predicate = fact.Predicate,
            Object = fact.Object,
            Confidence = fact.Confidence,
            LearnedAt = fact.LearnedAt,
            Source = fact.Source,
            EmbeddingVector = embedding,
            UserId = existing?.UserId ?? DefaultUserId,
            RelatedFactIds = related
        };

        await _repository.UpsertAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<KnowledgeFact>> QueryAsync(string query, int count = 10, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        if (count <= 0)
        {
            return Array.Empty<KnowledgeFact>();
        }

        var facts = await _repository.GetByUserAsync(DefaultUserId, cancellationToken).ConfigureAwait(false);
        var candidates = facts.Where(x => x.Confidence >= _options.MinConfidence).ToList();
        if (candidates.Count == 0)
        {
            return Array.Empty<KnowledgeFact>();
        }

        if (_knowHubBridge.IsAvailable)
        {
            try
            {
                var bridgeResults = await _knowHubBridge.SearchKnowledgeAsync(query, count * 3, cancellationToken).ConfigureAwait(false);
                var ranked = RankWithBridgeResults(candidates, bridgeResults)
                    .Take(count)
                    .Select(ToContract)
                    .ToList();

                if (ranked.Count > 0)
                {
                    return ranked;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Semantic query bridge search failed; falling back to local text matching.");
            }
        }

        return candidates
            .Select(x => new { Fact = x, Score = ComputeTextScore(query, x) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Fact.Confidence)
            .Take(count)
            .Select(x => ToContract(x.Fact))
            .ToList();
    }

    public async Task<IReadOnlyList<KnowledgeFact>> GetRelatedAsync(string factId, int count = 5, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factId);

        if (count <= 0)
        {
            return Array.Empty<KnowledgeFact>();
        }

        if (!_knowHubBridge.IsAvailable)
        {
            return Array.Empty<KnowledgeFact>();
        }

        var root = await _repository.GetByFactIdAsync(factId, cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return Array.Empty<KnowledgeFact>();
        }

        try
        {
            var graphResult = await _knowHubBridge
                .QueryGraphAsync(BuildRelatedGraphQuery(root), root.UserId, Math.Max(count * 3, count), cancellationToken)
                .ConfigureAwait(false);

            var allFacts = await _repository.GetByUserAsync(root.UserId, cancellationToken).ConfigureAwait(false);
            var relatedByGraph = allFacts
                .Where(x => !string.Equals(x.FactId, root.FactId, StringComparison.Ordinal))
                .Where(x =>
                    root.RelatedFactIds.Contains(x.FactId, StringComparer.Ordinal)
                    || MatchesRelevantEntity(x, graphResult.RelevantEntities))
                .OrderByDescending(x => x.Confidence)
                .Take(count)
                .Select(ToContract)
                .ToList();

            return relatedByGraph;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic graph query for related facts failed for {FactId}; returning empty list.", factId);
            return Array.Empty<KnowledgeFact>();
        }
    }

    public async Task UpdateFactAsync(KnowledgeFact fact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ValidateFact(fact);

        var existing = await _repository.GetByFactIdAsync(fact.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            throw new InvalidOperationException($"Knowledge fact '{fact.Id}' was not found.");
        }

        float[]? embedding = existing.EmbeddingVector;
        if (_knowHubBridge.IsAvailable)
        {
            try
            {
                embedding = await _knowHubBridge.GenerateEmbeddingAsync(BuildFactText(fact), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding refresh failed for semantic fact {FactId}; preserving previous embedding.", fact.Id);
            }
        }

        var allFacts = await _repository.GetByUserAsync(existing.UserId, cancellationToken).ConfigureAwait(false);
        var related = FindRelatedFactIds(fact, allFacts, fact.Id);

        var updated = new SemanticFactEntity
        {
            Id = existing.Id,
            FactId = fact.Id,
            Subject = fact.Subject,
            Predicate = fact.Predicate,
            Object = fact.Object,
            Confidence = fact.Confidence,
            LearnedAt = fact.LearnedAt,
            Source = fact.Source,
            EmbeddingVector = embedding,
            UserId = existing.UserId,
            RelatedFactIds = related
        };

        await _repository.UpsertAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<KnowledgeFact>> GetGraphAsync(string rootFactId, int depth = 2, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFactId);

        if (depth <= 0)
        {
            return Array.Empty<KnowledgeFact>();
        }

        if (!_knowHubBridge.IsAvailable)
        {
            return Array.Empty<KnowledgeFact>();
        }

        var root = await _repository.GetByFactIdAsync(rootFactId, cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return Array.Empty<KnowledgeFact>();
        }

        var boundedDepth = Math.Min(depth, _options.GraphTraversalMaxDepth);

        try
        {
            var graphResult = await _knowHubBridge
                .QueryGraphAsync(BuildSubgraphQuery(root, boundedDepth), root.UserId, Math.Max(25, boundedDepth * 25), cancellationToken)
                .ConfigureAwait(false);

            var allFacts = await _repository.GetByUserAsync(root.UserId, cancellationToken).ConfigureAwait(false);
            var byFactId = allFacts.ToDictionary(x => x.FactId, StringComparer.Ordinal);

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<(string FactId, int Depth)>();
            var graph = new List<KnowledgeFact>();

            queue.Enqueue((root.FactId, 0));

            while (queue.Count > 0)
            {
                var (currentFactId, currentDepth) = queue.Dequeue();
                if (!visited.Add(currentFactId))
                {
                    continue;
                }

                if (!byFactId.TryGetValue(currentFactId, out var current))
                {
                    continue;
                }

                graph.Add(ToContract(current));

                if (currentDepth >= boundedDepth)
                {
                    continue;
                }

                foreach (var relatedId in current.RelatedFactIds)
                {
                    if (!visited.Contains(relatedId))
                    {
                        queue.Enqueue((relatedId, currentDepth + 1));
                    }
                }

                foreach (var candidate in byFactId.Values)
                {
                    if (visited.Contains(candidate.FactId))
                    {
                        continue;
                    }

                    if (candidate.RelatedFactIds.Contains(currentFactId, StringComparer.Ordinal)
                        || MatchesRelevantEntity(candidate, graphResult.RelevantEntities))
                    {
                        queue.Enqueue((candidate.FactId, currentDepth + 1));
                    }
                }
            }

            return graph;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic graph traversal failed for root fact {RootFactId}; returning empty list.", rootFactId);
            return Array.Empty<KnowledgeFact>();
        }
    }

    private static string BuildFactText(KnowledgeFact fact) =>
        $"{fact.Subject} {fact.Predicate} {fact.Object}";

    private static string BuildRelatedGraphQuery(SemanticFactEntity root) =>
        $"Find facts related to: {root.Subject} {root.Predicate} {root.Object}";

    private static string BuildSubgraphQuery(SemanticFactEntity root, int depth) =>
        $"Traverse graph from fact: {root.Subject} {root.Predicate} {root.Object}. Depth: {depth}";

    private static KnowledgeFact ToContract(SemanticFactEntity entity) =>
        new(
            entity.FactId,
            entity.Subject,
            entity.Predicate,
            entity.Object,
            entity.Confidence,
            entity.LearnedAt,
            entity.Source);

    private static bool MatchesRelevantEntity(SemanticFactEntity entity, IReadOnlyList<string> relevantEntities)
    {
        if (relevantEntities.Count == 0)
        {
            return false;
        }

        foreach (var relevant in relevantEntities)
        {
            if (string.IsNullOrWhiteSpace(relevant))
            {
                continue;
            }

            if (entity.FactId.Contains(relevant, StringComparison.OrdinalIgnoreCase)
                || entity.Subject.Contains(relevant, StringComparison.OrdinalIgnoreCase)
                || entity.Object.Contains(relevant, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> FindRelatedFactIds(
        KnowledgeFact fact,
        IReadOnlyList<SemanticFactEntity> allFacts,
        string currentFactId)
    {
        var related = allFacts
            .Where(x => !string.Equals(x.FactId, currentFactId, StringComparison.Ordinal))
            .Where(x =>
                string.Equals(x.Subject, fact.Subject, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Object, fact.Object, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Subject, fact.Object, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Object, fact.Subject, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.FactId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return related;
    }

    private static double ComputeTextScore(string query, SemanticFactEntity entity)
    {
        var queryTokens = Tokenize(query);
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        var factTokens = Tokenize($"{entity.Subject} {entity.Predicate} {entity.Object} {entity.Source ?? string.Empty}");
        if (factTokens.Count == 0)
        {
            return 0;
        }

        var overlap = queryTokens.Count(token => factTokens.Contains(token));
        if (overlap == 0)
        {
            return 0;
        }

        return overlap / (double)queryTokens.Count;
    }

    private static HashSet<string> Tokenize(string text)
    {
        return text
            .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '-', '_', '/', '\\', '|', '(', ')', '[', ']', '{', '}' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<SemanticFactEntity> RankWithBridgeResults(
        IReadOnlyList<SemanticFactEntity> candidates,
        IReadOnlyList<KnowledgeSearchResult> bridgeResults)
    {
        if (bridgeResults.Count == 0)
        {
            return Array.Empty<SemanticFactEntity>();
        }

        var ranked = candidates
            .Select(candidate => new
            {
                Fact = candidate,
                Score = bridgeResults
                    .Select(result => ComputeBridgeScore(candidate, result))
                    .DefaultIfEmpty(0f)
                    .Max()
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Fact.Confidence)
            .Select(x => x.Fact)
            .ToList();

        return ranked;
    }

    private static float ComputeBridgeScore(SemanticFactEntity entity, KnowledgeSearchResult result)
    {
        var overlap = Tokenize($"{entity.Subject} {entity.Predicate} {entity.Object}")
            .Intersect(Tokenize(result.ChunkText), StringComparer.Ordinal)
            .Count();

        var overlapBoost = overlap > 0 ? Math.Min(0.2f, overlap * 0.05f) : 0f;
        return result.Similarity + overlapBoost;
    }

    private static void ValidateFact(KnowledgeFact fact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fact.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(fact.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(fact.Predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(fact.Object);

        if (fact.Confidence is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(fact), "Confidence must be in range [0, 1].");
        }
    }
}
