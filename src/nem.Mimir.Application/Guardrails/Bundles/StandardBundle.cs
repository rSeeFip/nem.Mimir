namespace nem.Mimir.Application.Guardrails.Bundles;

using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Guardrails.Rules;

public sealed class StandardBundle : IGuardrailBundle
{
    private static readonly IReadOnlyList<IGuardrailRule> StandardRules = Array.AsReadOnly<IGuardrailRule>(
    [
        new KnownBadPatternRule(),
        new OutputLengthRule(),
    ]);

    public string Name => "standard";

    public GuardrailLevel Level => GuardrailLevel.Standard;

    public IReadOnlyList<IGuardrailRule> Rules => StandardRules;
}
