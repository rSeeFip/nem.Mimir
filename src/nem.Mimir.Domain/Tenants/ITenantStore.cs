namespace nem.Mimir.Domain.Tenants;

public interface ITenantStore
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);

    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
