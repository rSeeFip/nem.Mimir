namespace nem.Mimir.Application.Guardrails.Rules;

using System.Text.RegularExpressions;
using nem.Contracts.TokenOptimization;

public sealed partial class SensitiveDataRule : IGuardrailRule
{
    public const string Id = "sensitive-data";

    public string RuleId => Id;

    public Task<RuleResult> EvaluateAsync(GuardrailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var text = string.Concat(request.Prompt, " ", request.Response);
        var containsSensitiveData = EmailRegex().IsMatch(text)
            || PhoneRegex().IsMatch(text)
            || SocialSecurityRegex().IsMatch(text);

        var result = containsSensitiveData
            ? new RuleResult(RuleId, false, "Sensitive data pattern detected.", GuardrailLevel.Strict)
            : new RuleResult(RuleId, true, null, GuardrailLevel.Strict);

        return Task.FromResult(result);
    }

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b(?:\+?1[-.\s]?)?(?:\(?\d{3}\)?[-.\s]?){2}\d{4}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SocialSecurityRegex();
}
