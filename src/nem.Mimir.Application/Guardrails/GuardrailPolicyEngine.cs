namespace nem.Mimir.Application.Guardrails;

using nem.Contracts.TokenOptimization;
using Microsoft.Extensions.Options;

public sealed class GuardrailPolicyEngine : IGuardrailEvaluator
{
    private readonly GuardrailsOptions _options;

    public GuardrailPolicyEngine(IOptions<GuardrailsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? new GuardrailsOptions();
    }

    public async Task<GuardrailResult> EvaluateAsync(GuardrailRequest request, IGuardrailBundle bundle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bundle);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled)
        {
            return new GuardrailResult(true, bundle.Level, [], null);
        }

        if (bundle.Rules.Count == 0)
        {
            return new GuardrailResult(true, bundle.Level, [], null);
        }

        var results = new List<RuleResult>(bundle.Rules.Count);

        foreach (var rule in bundle.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await rule.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            if (!result.Passed && bundle.Level == GuardrailLevel.Strict)
            {
                return new GuardrailResult(false, bundle.Level, results, BuildReason(results));
            }
        }

        var allowed = results.All(static x => x.Passed);
        return new GuardrailResult(allowed, bundle.Level, results, allowed ? null : BuildReason(results));
    }

    private static string? BuildReason(IReadOnlyCollection<RuleResult> results)
    {
        var failures = results
            .Where(static x => !x.Passed)
            .Select(static x => string.IsNullOrWhiteSpace(x.Message) ? x.RuleId : $"{x.RuleId}: {x.Message}")
            .ToArray();

        return failures.Length == 0 ? null : string.Join("; ", failures);
    }
}
