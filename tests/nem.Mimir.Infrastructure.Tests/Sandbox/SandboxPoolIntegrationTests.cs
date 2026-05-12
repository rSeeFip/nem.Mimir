using Microsoft.Extensions.DependencyInjection;
using nem.Contracts.Sandbox;
using nem.Mimir.Infrastructure.Sandbox;
using NSubstitute;

namespace nem.Mimir.Infrastructure.Tests.Sandbox;

public sealed class SandboxPoolIntegrationTests
{
    [Fact]
    public async Task AcquireSessionAsync_ReusesPreWarmedSession_FromPool()
    {
        var sandboxContract = Substitute.For<ISandboxContract>();
        var config = new SandboxConfig("python:3.12");
        var pooledSession = CreateSession("pooled-session", config);

        sandboxContract.AcquireAsync(config, Arg.Any<CancellationToken>()).Returns(pooledSession);

        var sut = new OpenSandboxPoolManager(sandboxContract);
        await sut.WarmPoolAsync(1, config, cancellationToken: CancellationToken.None);

        var acquired = await sut.AcquireSessionAsync(config, cancellationToken: CancellationToken.None);

        Assert.Same(pooledSession, acquired);
        await sandboxContract.Received(1).AcquireAsync(config, CancellationToken.None);

        var stats = await sut.GetPoolStatsAsync();
        Assert.Equal(1, stats.ActiveSessions);
        Assert.Equal(0, stats.IdleSessions);
        Assert.Equal(1, stats.TotalCreated);
        Assert.Equal(0, stats.TotalDestroyed);
    }

    [Fact]
    public async Task ReleaseSessionAsync_ReturnsSessionToPool_ForNextAcquire()
    {
        var sandboxContract = Substitute.For<ISandboxContract>();
        var config = new SandboxConfig("python:3.12");
        var firstSession = CreateSession("session-1", config);

        sandboxContract.AcquireAsync(config, Arg.Any<CancellationToken>()).Returns(firstSession);

        var sut = new OpenSandboxPoolManager(sandboxContract);
        var acquired = await sut.AcquireSessionAsync(config, cancellationToken: CancellationToken.None);
        await sut.ReleaseSessionAsync(acquired, CancellationToken.None);
        var reacquired = await sut.AcquireSessionAsync(config, cancellationToken: CancellationToken.None);

        Assert.Same(firstSession, reacquired);
        await sandboxContract.Received(1).AcquireAsync(config, CancellationToken.None);

        var stats = await sut.GetPoolStatsAsync();
        Assert.Equal(1, stats.ActiveSessions);
        Assert.Equal(0, stats.IdleSessions);
        Assert.Equal(1, stats.TotalCreated);
        Assert.Equal(0, stats.TotalDestroyed);
    }

    [Fact]
    public async Task AcquireSessionAsync_WhenPoolExhausted_CreatesNewSessionViaContract()
    {
        var sandboxContract = Substitute.For<ISandboxContract>();
        var config = new SandboxConfig("node:20");
        var pooledSession = CreateSession("session-1", config);
        var overflowSession = CreateSession("session-2", config);

        sandboxContract.AcquireAsync(config, Arg.Any<CancellationToken>()).Returns(pooledSession, overflowSession);

        var sut = new OpenSandboxPoolManager(sandboxContract);
        await sut.WarmPoolAsync(1, config, cancellationToken: CancellationToken.None);

        var first = await sut.AcquireSessionAsync(config, cancellationToken: CancellationToken.None);
        var second = await sut.AcquireSessionAsync(config, cancellationToken: CancellationToken.None);

        Assert.Same(pooledSession, first);
        Assert.Same(overflowSession, second);
        await sandboxContract.Received(2).AcquireAsync(config, CancellationToken.None);

        var stats = await sut.GetPoolStatsAsync();
        Assert.Equal(2, stats.ActiveSessions);
        Assert.Equal(0, stats.IdleSessions);
        Assert.Equal(2, stats.TotalCreated);
        Assert.Equal(0, stats.TotalDestroyed);
    }

    [Fact]
    public async Task DrainPoolAsync_ReleasesIdleSessions_ThroughContract()
    {
        var sandboxContract = Substitute.For<ISandboxContract>();
        var config = new SandboxConfig("python:3.12");
        var firstSession = CreateSession("session-1", config);
        var secondSession = CreateSession("session-2", config);

        sandboxContract.AcquireAsync(config, Arg.Any<CancellationToken>()).Returns(firstSession, secondSession);

        var sut = new OpenSandboxPoolManager(sandboxContract);
        await sut.WarmPoolAsync(2, config, cancellationToken: CancellationToken.None);

        await sut.DrainPoolAsync(CancellationToken.None);

        await sandboxContract.Received(1).ReleaseAsync(new SandboxSessionId("session-1"), CancellationToken.None);
        await sandboxContract.Received(1).ReleaseAsync(new SandboxSessionId("session-2"), CancellationToken.None);

        var stats = await sut.GetPoolStatsAsync();
        Assert.Equal(0, stats.ActiveSessions);
        Assert.Equal(0, stats.IdleSessions);
        Assert.Equal(2, stats.TotalCreated);
        Assert.Equal(2, stats.TotalDestroyed);
    }

    [Fact]
    public async Task DrainPoolAsync_DoesNotDisposeSessions_AfterContractRelease()
    {
        var sandboxContract = Substitute.For<ISandboxContract>();
        var config = new SandboxConfig("python:3.12");
        var session = Substitute.For<ISandboxSession, IAsyncDisposable>();
        session.SessionId.Returns("session-1");
        session.Config.Returns(config);

        sandboxContract.AcquireAsync(config, Arg.Any<CancellationToken>()).Returns(session);

        var sut = new OpenSandboxPoolManager(sandboxContract);
        await sut.WarmPoolAsync(1, config, cancellationToken: CancellationToken.None);

        await sut.DrainPoolAsync(CancellationToken.None);

        await ((IAsyncDisposable)session).DidNotReceive().DisposeAsync();
        await sandboxContract.Received(1).ReleaseAsync(new SandboxSessionId("session-1"), CancellationToken.None);
    }

    [Fact]
    public async Task SandboxContractAdapter_DelegatesAcquireAndRelease_ToProvider()
    {
        var provider = Substitute.For<ISandboxProvider>();
        var session = CreateSession("session-abc", new SandboxConfig("python:3.12"));
        var config = new SandboxConfig("python:3.12");

        provider.CreateSessionAsync(config, SandboxPolicyClass.CreationLocked, CancellationToken.None).Returns(session);
        provider.DestroySessionAsync("session-abc", CancellationToken.None).Returns(Task.CompletedTask);

        var adapter = new SandboxContractAdapter(provider);

        var acquired = await adapter.AcquireAsync(config, CancellationToken.None);
        await adapter.ReleaseAsync(new SandboxSessionId("session-abc"), CancellationToken.None);

        Assert.Same(session, acquired);
        await provider.Received(1).CreateSessionAsync(config, SandboxPolicyClass.CreationLocked, CancellationToken.None);
        await provider.Received(1).DestroySessionAsync("session-abc", CancellationToken.None);
    }

    [Fact]
    public void AddOpenSandboxProvider_RegistersSandboxContractAdapter_ForPoolManager()
    {
        var services = new ServiceCollection();

        services.AddOpenSandboxProvider(new OpenSandboxOptions
        {
            BaseUrl = "https://sandbox.example/",
            TimeoutSeconds = 120,
        });

        using var serviceProvider = services.BuildServiceProvider();

        var sandboxContract = serviceProvider.GetRequiredService<ISandboxContract>();
        var poolManager = serviceProvider.GetRequiredService<OpenSandboxPoolManager>();

        Assert.IsType<SandboxContractAdapter>(sandboxContract);
        Assert.NotNull(poolManager);
    }

    private static ISandboxSession CreateSession(string sessionId, SandboxConfig config)
    {
        var session = Substitute.For<ISandboxSession>();
        session.SessionId.Returns(sessionId);
        session.Config.Returns(config);
        return session;
    }
}
