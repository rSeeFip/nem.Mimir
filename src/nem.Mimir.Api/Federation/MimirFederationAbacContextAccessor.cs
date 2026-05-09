using nem.Contracts.Federation.Authorization;

namespace nem.Mimir.Api.Federation;

public sealed class MimirFederationAbacContextAccessor
{
    private static readonly AsyncLocal<ClassificationAuthorizationContext?> CurrentContext = new();

    public ClassificationAuthorizationContext? Current
    {
        get => CurrentContext.Value;
        set => CurrentContext.Value = value;
    }
}
