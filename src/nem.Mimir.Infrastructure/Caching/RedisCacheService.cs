namespace nem.Mimir.Infrastructure.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class RedisCacheService : IDistributedCache
{
    private readonly IDistributedCache _inner;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly CacheOptions _options;

    public RedisCacheService(
        IDistributedCache inner,
        ILogger<RedisCacheService> logger,
        IOptions<CacheOptions> options)
    {
        _inner = inner;
        _logger = logger;
        _options = options.Value;
    }

    public CacheOptions Options => _options;

    public byte[]? Get(string key)
    {
        try
        {
            return _inner.Get(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for key {Key}", key);
            return null;
        }
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        try
        {
            return await _inner.GetAsync(key, token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for key {Key}", key);
            return null;
        }
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        try
        {
            _inner.Set(key, value, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key {Key}", key);
        }
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        try
        {
            await _inner.SetAsync(key, value, options, token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key {Key}", key);
        }
    }

    public void Refresh(string key)
    {
        try
        {
            _inner.Refresh(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REFRESH failed for key {Key}", key);
        }
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        try
        {
            await _inner.RefreshAsync(key, token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REFRESH failed for key {Key}", key);
        }
    }

    public void Remove(string key)
    {
        try
        {
            _inner.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        try
        {
            await _inner.RemoveAsync(key, token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for key {Key}", key);
        }
    }
}
