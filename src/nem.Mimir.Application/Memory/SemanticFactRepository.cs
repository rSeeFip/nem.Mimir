using System.Collections.Concurrent;

namespace nem.Mimir.Application.Services.Memory;

public interface ISemanticFactRepository
{
    Task UpsertAsync(SemanticFactEntity entity, CancellationToken cancellationToken = default);
    Task<SemanticFactEntity?> GetByFactIdAsync(string factId, CancellationToken cancellationToken = default);
    Task<int> CountByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SemanticFactEntity>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class InMemorySemanticFactRepository : ISemanticFactRepository
{
    private readonly ConcurrentDictionary<string, SemanticFactEntity> _facts = new(StringComparer.Ordinal);

    public Task UpsertAsync(SemanticFactEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        _facts[entity.FactId] = entity;
        return Task.CompletedTask;
    }

    public Task<SemanticFactEntity?> GetByFactIdAsync(string factId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _facts.TryGetValue(factId, out var entity);
        return Task.FromResult(entity);
    }

    public Task<int> CountByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = _facts.Values.Count(x => string.Equals(x.UserId, userId, StringComparison.Ordinal));
        return Task.FromResult(count);
    }

    public Task<IReadOnlyList<SemanticFactEntity>> GetByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var facts = _facts.Values
            .Where(x => string.Equals(x.UserId, userId, StringComparison.Ordinal))
            .ToList();

        return Task.FromResult<IReadOnlyList<SemanticFactEntity>>(facts);
    }
}

public sealed class SemanticFactEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string FactId { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Predicate { get; init; } = string.Empty;

    public string Object { get; init; } = string.Empty;

    public float Confidence { get; init; } = 1.0f;

    public DateTimeOffset? LearnedAt { get; init; }

    public string? Source { get; init; }

    public float[]? EmbeddingVector { get; init; }

    public string UserId { get; init; } = string.Empty;

    public List<string> RelatedFactIds { get; init; } = new();
}
