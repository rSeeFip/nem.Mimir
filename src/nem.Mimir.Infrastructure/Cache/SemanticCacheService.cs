namespace nem.Mimir.Infrastructure.Cache;

using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using nem.Contracts.TokenOptimization;

internal sealed class SemanticCacheService(
    SemanticCacheOptions options,
    ILogger<SemanticCacheService> logger,
    IHttpContextAccessor? httpContextAccessor = null) : ISemanticCache
{
    private const float MinimumSimilarityThreshold = 0.90f;
    private const string GlobalScope = "global";
    private const string AnonymousScope = "anonymous";
    private const string ScopeSeparator = "::";
    private const int NGramSize = 3;

    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

    private long _totalRequests;
    private long _hits;
    private long _misses;

    public Task<string?> GetAsync(string key, float similarityThreshold = 0.95f, CancellationToken cancellationToken = default)
    {
        try
        {
            Interlocked.Increment(ref _totalRequests);

            if (string.IsNullOrWhiteSpace(key))
            {
                Interlocked.Increment(ref _misses);
                return Task.FromResult<string?>(null);
            }

            var configuredThreshold = options.SimilarityThreshold <= 0f
                ? MinimumSimilarityThreshold
                : options.SimilarityThreshold;

            var requestedThreshold = similarityThreshold <= 0f
                ? configuredThreshold
                : similarityThreshold;

            var effectiveThreshold = Math.Max(MinimumSimilarityThreshold, requestedThreshold);
            var normalizedKey = Normalize(key);
            var scope = GetScope();
            var queryVector = BuildNGramVector(normalizedKey);
            var queryNorm = CalculateNorm(queryVector);
            var now = DateTimeOffset.UtcNow;

            string? bestValue = null;
            var bestScore = 0f;
            List<string>? expiredKeys = null;

            foreach (var kvp in _entries)
            {
                var entry = kvp.Value;

                if (entry.ExpiresAt <= now)
                {
                    expiredKeys ??= [];
                    expiredKeys.Add(kvp.Key);
                    continue;
                }

                if (!string.Equals(entry.Scope, scope, StringComparison.Ordinal))
                {
                    continue;
                }

                var similarity = CosineSimilarity(queryVector, queryNorm, entry.Vector, entry.Norm);
                if (similarity > bestScore)
                {
                    bestScore = similarity;
                    bestValue = entry.Value;
                }
            }

            if (expiredKeys is not null)
            {
                foreach (var expiredKey in expiredKeys)
                {
                    _entries.TryRemove(expiredKey, out _);
                }
            }

            if (bestValue is not null && bestScore >= effectiveThreshold)
            {
                Interlocked.Increment(ref _hits);
                return Task.FromResult<string?>(bestValue);
            }

            Interlocked.Increment(ref _misses);
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _misses);
            logger.LogWarning(ex, "Semantic cache retrieval failed. Falling back to cache miss.");
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Task.CompletedTask;
            }

            var normalizedKey = Normalize(key);
            var scope = GetScope();
            var scopedKey = BuildScopedKey(scope, normalizedKey);
            var now = DateTimeOffset.UtcNow;
            var resolvedTtl = ttl ?? options.DefaultTtl;

            if (resolvedTtl <= TimeSpan.Zero)
            {
                _entries.TryRemove(scopedKey, out _);
                return Task.CompletedTask;
            }

            var vector = BuildNGramVector(normalizedKey);
            var norm = CalculateNorm(vector);

            _entries[scopedKey] = new CacheEntry(
                scope,
                normalizedKey,
                value,
                vector,
                norm,
                now,
                now.Add(resolvedTtl));

            var maxEntries = Math.Max(1, options.MaxEntries);
            var overflow = _entries.Count - maxEntries;
            if (overflow > 0)
            {
                foreach (var candidate in _entries
                             .OrderBy(x => x.Value.CreatedAt)
                             .Take(overflow))
                {
                    _entries.TryRemove(candidate.Key, out _);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Semantic cache storage failed. Ignoring cache write.");
        }

        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Task.CompletedTask;
            }

            var normalizedKey = Normalize(key);
            var scopedKey = BuildScopedKey(GetScope(), normalizedKey);
            _entries.TryRemove(scopedKey, out _);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Semantic cache invalidation failed. Ignoring cache invalidation request.");
        }

        return Task.CompletedTask;
    }

    public Task<CacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var estimatedSize = 0L;
            List<string>? expiredKeys = null;

            foreach (var kvp in _entries)
            {
                var entry = kvp.Value;
                if (entry.ExpiresAt <= now)
                {
                    expiredKeys ??= [];
                    expiredKeys.Add(kvp.Key);
                    continue;
                }

                estimatedSize += Encoding.UTF8.GetByteCount(entry.Key);
                estimatedSize += Encoding.UTF8.GetByteCount(entry.Value);
            }

            if (expiredKeys is not null)
            {
                foreach (var expiredKey in expiredKeys)
                {
                    _entries.TryRemove(expiredKey, out _);
                }
            }

            var totalRequests = Interlocked.Read(ref _totalRequests);
            var hits = Interlocked.Read(ref _hits);
            var misses = Interlocked.Read(ref _misses);
            var hitRate = totalRequests == 0 ? 0d : (double)hits / totalRequests;

            return Task.FromResult(new CacheStats(totalRequests, hits, misses, hitRate, estimatedSize));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Semantic cache stats collection failed. Returning safe defaults.");
            return Task.FromResult(new CacheStats(0, 0, 0, 0, 0));
        }
    }

    private string GetScope()
    {
        if (!options.EnablePerUserIsolation)
        {
            return GlobalScope;
        }

        var principal = httpContextAccessor?.HttpContext?.User;
        var userId = principal?.FindFirst("sub")?.Value
                     ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrWhiteSpace(userId)
            ? AnonymousScope
            : userId;
    }

    private static string BuildScopedKey(string scope, string normalizedKey) =>
        $"{scope}{ScopeSeparator}{normalizedKey}";

    private static string Normalize(string key) => key.Trim().ToLowerInvariant();

    private static Dictionary<string, int> BuildNGramVector(string text)
    {
        var vector = new Dictionary<string, int>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(text))
        {
            return vector;
        }

        if (text.Length < NGramSize)
        {
            foreach (var ch in text)
            {
                var token = ch.ToString();
                vector[token] = vector.TryGetValue(token, out var count) ? count + 1 : 1;
            }

            return vector;
        }

        for (var i = 0; i <= text.Length - NGramSize; i++)
        {
            var gram = text.Substring(i, NGramSize);
            vector[gram] = vector.TryGetValue(gram, out var count) ? count + 1 : 1;
        }

        return vector;
    }

    private static float CalculateNorm(IReadOnlyDictionary<string, int> vector)
    {
        if (vector.Count == 0)
        {
            return 0f;
        }

        double sumSquares = 0;
        foreach (var count in vector.Values)
        {
            sumSquares += count * count;
        }

        return (float)Math.Sqrt(sumSquares);
    }

    private static float CosineSimilarity(
        IReadOnlyDictionary<string, int> left,
        float leftNorm,
        IReadOnlyDictionary<string, int> right,
        float rightNorm)
    {
        if (leftNorm == 0f || rightNorm == 0f)
        {
            return 0f;
        }

        var smaller = left.Count <= right.Count ? left : right;
        var larger = ReferenceEquals(smaller, left) ? right : left;

        double dot = 0;
        foreach (var (token, count) in smaller)
        {
            if (larger.TryGetValue(token, out var rightCount))
            {
                dot += count * rightCount;
            }
        }

        return (float)(dot / (leftNorm * rightNorm));
    }

    private sealed record CacheEntry(
        string Scope,
        string Key,
        string Value,
        IReadOnlyDictionary<string, int> Vector,
        float Norm,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt);
}
