namespace nem.Mimir.Infrastructure.Tests.Caching;

using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using nem.Mimir.Infrastructure.Caching;
using Shouldly;

public sealed class RedisCacheServiceTests
{
    private static RedisCacheService CreateSut(CacheOptions? options = null)
    {
        var inner = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var logger = NullLogger<RedisCacheService>.Instance;
        var cacheOptions = Options.Create(options ?? new CacheOptions());
        return new RedisCacheService(inner, logger, cacheOptions);
    }

    [Fact]
    public async Task SetAsync_then_GetAsync_returns_stored_value()
    {
        var sut = CreateSut();
        var key = "test-key";
        var value = Encoding.UTF8.GetBytes("hello");
        var entryOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };

        await sut.SetAsync(key, value, entryOptions);
        var result = await sut.GetAsync(key);

        result.ShouldNotBeNull();
        Encoding.UTF8.GetString(result).ShouldBe("hello");
    }

    [Fact]
    public async Task GetAsync_missing_key_returns_null()
    {
        var sut = CreateSut();

        var result = await sut.GetAsync("nonexistent-key");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAsync_invalidates_entry()
    {
        var sut = CreateSut();
        var key = "remove-key";
        var value = Encoding.UTF8.GetBytes("data");
        var entryOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

        await sut.SetAsync(key, value, entryOptions);
        await sut.RemoveAsync(key);
        var result = await sut.GetAsync(key);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_overwrites_existing_entry()
    {
        var sut = CreateSut();
        var key = "overwrite-key";
        var entryOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

        await sut.SetAsync(key, Encoding.UTF8.GetBytes("first"), entryOptions);
        await sut.SetAsync(key, Encoding.UTF8.GetBytes("second"), entryOptions);
        var result = await sut.GetAsync(key);

        Encoding.UTF8.GetString(result!).ShouldBe("second");
    }

    [Fact]
    public void Options_exposes_configured_ttls()
    {
        var customOptions = new CacheOptions
        {
            ModelsTtl = TimeSpan.FromSeconds(30),
            BillingTtl = TimeSpan.FromSeconds(10),
            ConfigTtl = TimeSpan.FromSeconds(60),
        };
        var sut = CreateSut(customOptions);

        sut.Options.ModelsTtl.ShouldBe(TimeSpan.FromSeconds(30));
        sut.Options.BillingTtl.ShouldBe(TimeSpan.FromSeconds(10));
        sut.Options.ConfigTtl.ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void Default_options_have_expected_ttls()
    {
        var sut = CreateSut();

        sut.Options.ModelsTtl.ShouldBe(TimeSpan.FromMinutes(5));
        sut.Options.BillingTtl.ShouldBe(TimeSpan.FromMinutes(1));
        sut.Options.ConfigTtl.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task GetAsync_after_expiry_returns_null()
    {
        var sut = CreateSut();
        var key = "expiry-key";
        var value = Encoding.UTF8.GetBytes("ephemeral");
        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(50)
        };

        await sut.SetAsync(key, value, entryOptions);
        await Task.Delay(100);
        var result = await sut.GetAsync(key);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_swallows_inner_exception_and_does_not_throw()
    {
        var failingInner = new ThrowingDistributedCache();
        var logger = NullLogger<RedisCacheService>.Instance;
        var sut = new RedisCacheService(failingInner, logger, Options.Create(new CacheOptions()));

        await Should.NotThrowAsync(() =>
            sut.SetAsync("k", Encoding.UTF8.GetBytes("v"), new DistributedCacheEntryOptions()));
    }

    [Fact]
    public async Task GetAsync_swallows_inner_exception_and_returns_null()
    {
        var failingInner = new ThrowingDistributedCache();
        var logger = NullLogger<RedisCacheService>.Instance;
        var sut = new RedisCacheService(failingInner, logger, Options.Create(new CacheOptions()));

        var result = await sut.GetAsync("k");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAsync_swallows_inner_exception_and_does_not_throw()
    {
        var failingInner = new ThrowingDistributedCache();
        var logger = NullLogger<RedisCacheService>.Instance;
        var sut = new RedisCacheService(failingInner, logger, Options.Create(new CacheOptions()));

        await Should.NotThrowAsync(() => sut.RemoveAsync("k"));
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("simulated failure");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("simulated failure");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("simulated failure");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new InvalidOperationException("simulated failure");
        public void Refresh(string key) => throw new InvalidOperationException("simulated failure");
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("simulated failure");
        public void Remove(string key) => throw new InvalidOperationException("simulated failure");
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("simulated failure");
    }
}
