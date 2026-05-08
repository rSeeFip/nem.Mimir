using Marten;
using nem.Mimir.Domain.Tenants;

namespace nem.Mimir.Infrastructure.Persistence;

public sealed class TenantStore(IDocumentStore documentStore) : ITenantStore
{
    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var session = documentStore.QuerySession();
        return await session.Query<Tenant>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var session = documentStore.QuerySession();
        return await session.Query<Tenant>()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        await using var session = documentStore.QuerySession();
        return await session.Query<Tenant>()
            .AnyAsync(x => x.Slug == slug, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await using var session = documentStore.LightweightSession();
        session.Store(tenant);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await using var session = documentStore.LightweightSession();
        session.Store(tenant);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
