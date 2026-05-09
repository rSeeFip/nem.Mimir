using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IKnowledgeCollectionRepository
{
    Task<KnowledgeCollection?> GetByIdAsync(KnowledgeCollectionId id, CancellationToken cancellationToken = default);

    Task<KnowledgeCollection?> GetByIdForUserAsync(KnowledgeCollectionId id, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeCollection>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<KnowledgeCollection> CreateAsync(KnowledgeCollection collection, CancellationToken cancellationToken = default);

    Task UpdateAsync(KnowledgeCollection collection, CancellationToken cancellationToken = default);

    Task DeleteAsync(KnowledgeCollectionId id, Guid userId, CancellationToken cancellationToken = default);
}
