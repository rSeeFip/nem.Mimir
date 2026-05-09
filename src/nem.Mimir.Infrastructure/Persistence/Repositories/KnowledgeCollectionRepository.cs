namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

internal sealed class KnowledgeCollectionRepository(MimirDbContext context) : IKnowledgeCollectionRepository
{
    public async Task<KnowledgeCollection?> GetByIdAsync(KnowledgeCollectionId id, CancellationToken cancellationToken = default)
    {
        return await context.KnowledgeCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<KnowledgeCollection?> GetByIdForUserAsync(KnowledgeCollectionId id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.KnowledgeCollections
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<KnowledgeCollection>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.KnowledgeCollections
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<KnowledgeCollection> CreateAsync(KnowledgeCollection collection, CancellationToken cancellationToken = default)
    {
        await context.KnowledgeCollections
            .AddAsync(collection, cancellationToken)
            .ConfigureAwait(false);

        return collection;
    }

    public Task UpdateAsync(KnowledgeCollection collection, CancellationToken cancellationToken = default)
    {
        context.KnowledgeCollections.Update(collection);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(KnowledgeCollectionId id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await context.KnowledgeCollections
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is not null)
        {
            context.KnowledgeCollections.Remove(entity);
        }
    }
}
