namespace nem.Mimir.Infrastructure.Sandbox;

using nem.Contracts.Sandbox;

public sealed class SandboxContractAdapter : ISandboxContract
{
    private readonly ISandboxProvider _provider;

    public SandboxContractAdapter(ISandboxProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    public Task<ISandboxSession> AcquireAsync(SandboxConfig config, CancellationToken ct)
        => _provider.CreateSessionAsync(config, SandboxPolicyClass.CreationLocked, ct);

    public Task ReleaseAsync(SandboxSessionId id, CancellationToken ct)
        => _provider.DestroySessionAsync(id.Value, ct);
}
