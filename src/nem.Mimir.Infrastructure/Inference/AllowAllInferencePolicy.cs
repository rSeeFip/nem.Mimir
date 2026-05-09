namespace nem.Mimir.Infrastructure.Inference;

using nem.Contracts.Inference;
using Microsoft.Extensions.Options;

internal sealed class AllowAllInferencePolicy : IInferencePolicy
{
    private readonly InferencePolicyOptions _options;

    public AllowAllInferencePolicy(IOptions<InferencePolicyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? new InferencePolicyOptions();
    }

    public Task<PolicyResult> EvaluateAsync(InferenceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
        {
            return Task.FromResult(new PolicyResult(PolicyAction.Allow));
        }

        return Task.FromResult(new PolicyResult(_options.Action, _options.RedirectAlias, _options.DenialReason));
    }
}
