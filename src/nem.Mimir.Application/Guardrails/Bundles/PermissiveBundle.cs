namespace nem.Mimir.Application.Guardrails.Bundles;

using nem.Contracts.TokenOptimization;

public sealed class PermissiveBundle : IGuardrailBundle
{
    public string Name => "permissive";

    public GuardrailLevel Level => GuardrailLevel.Permissive;

    public IReadOnlyList<IGuardrailRule> Rules { get; } = [];
}
