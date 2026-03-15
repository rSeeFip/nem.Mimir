namespace nem.Mimir.Infrastructure.Sandbox;

using System.Collections.Concurrent;
using nem.Contracts.Sandbox;

public sealed class OpenSandboxPoolManager
{
    private readonly ISandboxProvider _provider;
    private readonly ConcurrentBag<ISandboxSession> _pool = [];
    private int _totalCreated;
    private int _totalDestroyed;

    public OpenSandboxPoolManager(ISandboxProvider provider)
    {
        _provider = provider;
    }

    public async Task WarmPoolAsync(int count, SandboxConfig config, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var session = await _provider.CreateSessionAsync(config, cancellationToken).ConfigureAwait(false);
            _pool.Add(session);
            Interlocked.Increment(ref _totalCreated);
        }
    }

    public Task<ISandboxSession> AcquireSessionAsync(SandboxConfig config, CancellationToken cancellationToken = default)
    {
        if (_pool.TryTake(out var session))
        {
            return Task.FromResult(session);
        }

        return CreateNewSessionAsync(config, cancellationToken);
    }

    public Task ReleaseSessionAsync(ISandboxSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _pool.Add(session);
        return Task.CompletedTask;
    }

    public Task<OpenSandboxPoolStats> GetPoolStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new OpenSandboxPoolStats(
            ActiveSessions: Math.Max(_totalCreated - _pool.Count - _totalDestroyed, 0),
            IdleSessions: _pool.Count,
            TotalCreated: _totalCreated,
            TotalDestroyed: _totalDestroyed);

        return Task.FromResult(stats);
    }

    public async Task DrainPoolAsync(CancellationToken cancellationToken = default)
    {
        while (_pool.TryTake(out var session))
        {
            await _provider.DestroySessionAsync(session.SessionId, cancellationToken).ConfigureAwait(false);
            if (session is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }

            Interlocked.Increment(ref _totalDestroyed);
        }
    }

    private async Task<ISandboxSession> CreateNewSessionAsync(SandboxConfig config, CancellationToken cancellationToken)
    {
        var session = await _provider.CreateSessionAsync(config, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _totalCreated);
        return session;
    }
}

public sealed record OpenSandboxPoolStats(
    int ActiveSessions,
    int IdleSessions,
    int TotalCreated,
    int TotalDestroyed);
