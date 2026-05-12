namespace nem.Mimir.Application.Guardrails.Rules;

using System.Text.RegularExpressions;
using nem.Contracts.TokenOptimization;

public sealed partial class KnownBadPatternRule : IGuardrailRule
{
    public const string Id = "known-bad-pattern";

    public string RuleId => Id;

    public Task<RuleResult> EvaluateAsync(GuardrailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var text = request.Prompt ?? string.Empty;
        var matched = KnownBadPatternRegex().IsMatch(text);
        var result = matched
            ? new RuleResult(RuleId, false, "Known prompt-injection or instruction-override pattern detected.", GuardrailLevel.Standard)
            : new RuleResult(RuleId, true, null, GuardrailLevel.Standard);

        return Task.FromResult(result);
    }

    [GeneratedRegex(@"ignore\s+(all|any|my|the)?\s*previous\s+instructions|ignore\s+all\s+instructions|disregard\s+(all|previous)\s+instructions|forget\s+(all|your|previous)\s+instructions|reveal\s+the\s+system\s+prompt|bypass\s+safety|override\s+instructions", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex KnownBadPatternRegex();
}
