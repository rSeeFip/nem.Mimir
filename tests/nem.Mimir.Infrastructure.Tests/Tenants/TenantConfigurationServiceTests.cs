using Marten;
using Marten.Linq;
using Microsoft.Extensions.Caching.Distributed;
using nem.Mimir.Domain.Tenants;
using nem.Mimir.Infrastructure.Caching;
using nem.Mimir.Infrastructure.Tenants;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Tenants;

public sealed class TenantConfigurationServiceTests
{
    private readonly IDocumentStore _documentStore = Substitute.For<IDocumentStore>();
    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();
    private readonly TenantConfigurationService _sut;

    public TenantConfigurationServiceTests()
    {
        _sut = new TenantConfigurationService(_documentStore, _cache);
    }

    [Fact]
    public async Task GetAsync_WhenCached_ReturnsCachedValue()
    {
        var tenantId = "tenant-1";
        var config = new TenantConfiguration { Id = Guid.NewGuid(), TenantId = tenantId, RateLimitPerMinute = 200 };
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(config);
        var cacheKey = TenantAwareCacheKey.Build(tenantId, TenantAwareCacheKey.Keys.TenantConfig);

        _cache.GetAsync(cacheKey, Arg.Any<CancellationToken>()).Returns(bytes);

        var result = await _sut.GetAsync(tenantId);

        result.ShouldNotBeNull();
        result!.TenantId.ShouldBe(tenantId);
        result.RateLimitPerMinute.ShouldBe(200);
        _documentStore.DidNotReceive().QuerySession();
    }

    [Fact]
    public async Task UpdateAsync_InvalidatesCache()
    {
        var tenantId = "tenant-2";
        var config = new TenantConfiguration { Id = Guid.NewGuid(), TenantId = tenantId };
        var cacheKey = TenantAwareCacheKey.Build(tenantId, TenantAwareCacheKey.Keys.TenantConfig);

        var session = Substitute.For<IDocumentSession>();
        _documentStore.LightweightSession().Returns(session);

        await _sut.UpdateAsync(config);

        await _cache.Received(1).RemoveAsync(cacheKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_WhenNotCached_OpensQuerySession()
    {
        var tenantId = "tenant-3";
        var cacheKey = TenantAwareCacheKey.Build(tenantId, TenantAwareCacheKey.Keys.TenantConfig);

        _cache.GetAsync(cacheKey, Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var martenQueryable = Substitute.For<IMartenQueryable<TenantConfiguration>>();
        martenQueryable.Expression.Returns(
            System.Linq.Expressions.Expression.Constant(Array.Empty<TenantConfiguration>().AsQueryable()));
        martenQueryable.ElementType.Returns(typeof(TenantConfiguration));
        martenQueryable.Provider.Returns(Array.Empty<TenantConfiguration>().AsQueryable().Provider);

        var querySession = Substitute.For<IQuerySession>();
        querySession.Query<TenantConfiguration>().Returns(martenQueryable);
        _documentStore.QuerySession().Returns(querySession);

        try
        {
            await _sut.GetAsync(tenantId);
        }
        catch (InvalidCastException)
        {
        }

        _documentStore.Received(1).QuerySession();
    }
}
