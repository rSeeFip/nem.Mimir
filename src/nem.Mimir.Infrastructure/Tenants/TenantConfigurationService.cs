using Marten;
using Microsoft.Extensions.Caching.Distributed;
using nem.Mimir.Application.Tenants;
using nem.Mimir.Domain.Tenants;
using nem.Mimir.Infrastructure.Caching;

namespace nem.Mimir.Infrastructure.Tenants;

public sealed class TenantConfigurationService(
    IDocumentStore documentStore,
    IDistributedCache cache) : ITenantConfigurationService
{
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
    };

    public async Task<TenantConfiguration?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = TenantAwareCacheKey.Build(tenantId, TenantAwareCacheKey.Keys.TenantConfig);
        var cached = await cache.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        if (cached is not null)
        {
            return System.Text.Json.JsonSerializer.Deserialize<TenantConfiguration>(cached);
        }

        await using var session = documentStore.QuerySession();
        var configuration = await session.Query<TenantConfiguration>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (configuration is not null)
        {
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(configuration);
            await cache.SetAsync(cacheKey, bytes, CacheOptions, cancellationToken).ConfigureAwait(false);
        }

        return configuration;
    }

    public async Task UpdateAsync(TenantConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var session = documentStore.LightweightSession();
        session.Store(configuration);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var cacheKey = TenantAwareCacheKey.Build(configuration.TenantId, TenantAwareCacheKey.Keys.TenantConfig);
        await cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
    }
}
