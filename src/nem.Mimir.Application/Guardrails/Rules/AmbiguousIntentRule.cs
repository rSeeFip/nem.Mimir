namespace nem.Mimir.Application.Guardrails.Rules;

using System.Text.RegularExpressions;
using nem.Contracts.TokenOptimization;

public sealed partial class AmbiguousIntentRule : IGuardrailRule
{
    public const string Id = "ambiguous-intent";

    public string RuleId => Id;

    public Task<RuleResult> EvaluateAsync(GuardrailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = request.Prompt?.Trim() ?? string.Empty;
        var isAmbiguous = string.IsNullOrWhiteSpace(prompt)
            || prompt.Length < 16
            || AmbiguousPatternRegex().IsMatch(prompt);

        var result = isAmbiguous
            ? new RuleResult(RuleId, false, "Request intent is ambiguous or underspecified.", GuardrailLevel.Strict)
            : new RuleResult(RuleId, true, null, GuardrailLevel.Strict);

        return Task.FromResult(result);
    }

    [GeneratedRegex(@"^(help|do it|fix it|something|anything|whatever|that|that thing|this thing|make it better|handle this)([.!?\s].*)?$|\b(help with that thing|do something with this|you know what i mean|figure it out)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AmbiguousPatternRegex();
}
