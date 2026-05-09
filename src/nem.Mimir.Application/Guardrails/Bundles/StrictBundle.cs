namespace nem.Mimir.Application.Guardrails.Bundles;

using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Guardrails.Rules;

public sealed class StrictBundle : IGuardrailBundle
{
    private static readonly IReadOnlyList<IGuardrailRule> StrictRules = Array.AsReadOnly<IGuardrailRule>(
    [
        new KnownBadPatternRule(),
        new OutputLengthRule(),
        new AmbiguousIntentRule(),
        new SensitiveDataRule(),
    ]);

    public string Name => "strict";

    public GuardrailLevel Level => GuardrailLevel.Strict;

    public IReadOnlyList<IGuardrailRule> Rules => StrictRules;
}
