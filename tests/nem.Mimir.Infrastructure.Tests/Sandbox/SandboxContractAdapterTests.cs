using nem.Contracts.Sandbox;
using nem.Mimir.Infrastructure.Sandbox;
using NSubstitute;

namespace nem.Mimir.Infrastructure.Tests.Sandbox;

public sealed class SandboxContractAdapterTests
{
    [Fact]
    public async Task AcquireAsync_DelegatesToProvider_AndReturnsSession()
    {
        var provider = Substitute.For<ISandboxProvider>();
        var session = Substitute.For<ISandboxSession>();
        var config = new SandboxConfig("python:3.12");

        provider
            .CreateSessionAsync(config, SandboxPolicyClass.CreationLocked, Arg.Any<CancellationToken>())
            .Returns(session);

        var adapter = new SandboxContractAdapter(provider);

        var result = await adapter.AcquireAsync(config, CancellationToken.None);

        Assert.Same(session, result);
        await provider.Received(1).CreateSessionAsync(config, SandboxPolicyClass.CreationLocked, CancellationToken.None);
    }

    [Fact]
    public async Task ReleaseAsync_DelegatesToProvider_WithSessionIdValue()
    {
        var provider = Substitute.For<ISandboxProvider>();
        var sessionId = new SandboxSessionId("session-abc");

        provider.DestroySessionAsync("session-abc", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var adapter = new SandboxContractAdapter(provider);

        await adapter.ReleaseAsync(sessionId, CancellationToken.None);

        await provider.Received(1).DestroySessionAsync("session-abc", CancellationToken.None);
    }

    [Fact]
    public async Task AcquireAsync_PassesCancellationToken_ToProvider()
    {
        var provider = Substitute.For<ISandboxProvider>();
        var session = Substitute.For<ISandboxSession>();
        var config = new SandboxConfig("node:20");
        using var cts = new CancellationTokenSource();

        provider
            .CreateSessionAsync(config, SandboxPolicyClass.CreationLocked, cts.Token)
            .Returns(session);

        var adapter = new SandboxContractAdapter(provider);

        var result = await adapter.AcquireAsync(config, cts.Token);

        Assert.Same(session, result);
        await provider.Received(1).CreateSessionAsync(config, SandboxPolicyClass.CreationLocked, cts.Token);
    }
}
