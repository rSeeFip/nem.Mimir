using nem.Contracts.Inference;

namespace nem.Mimir.Application.Llm;

public sealed class PolicyIntentOverlayService(
	IInferenceGateway innerGateway,
	IInferencePolicy defaultPolicy,
	IModelResolution modelResolution) : IPolicyIntentOverlay
{
	public Task<OverlayExecutionPlan> CreateExecutionPlanAsync(
		InferenceRequest request,
		CancellationToken ct = default)
	{
		return CreateExecutionPlanAsync(request, defaultPolicy, ct);
	}

	public Task<InferenceResponse> InferWithPolicyAsync(
		InferenceRequest request,
		CancellationToken ct = default)
	{
		return InferWithPolicyAsync(request, defaultPolicy, ct);
	}

	public async Task<InferenceResponse> InferWithPolicyAsync(
		InferenceRequest request,
		IInferencePolicy policy,
		CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(policy);

		var decision = await CreateExecutionPlanAsync(request, policy, ct).ConfigureAwait(false);

		if (decision.DenialReason is not null)
		{
			throw new InvalidOperationException(decision.DenialReason);
		}

		return await innerGateway.SendAsync(decision.Request, ct).ConfigureAwait(false);
	}

	public async Task<OverlayExecutionPlan> CreateExecutionPlanAsync(
		InferenceRequest request,
		IInferencePolicy policy,
		CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();

		var policyResult = await policy.EvaluateAsync(request, ct).ConfigureAwait(false);
		var overlayDecision = DetermineDecision(request, policyResult);

		if (overlayDecision.DenialReason is not null)
		{
			return new OverlayExecutionPlan(request, overlayDecision.DenialReason);
		}

		var context = new PolicyContext(ExtractTenantId(request.Metadata), request.Metadata);
		var resolvedModel = await modelResolution.ResolveAsync(overlayDecision.AliasToResolve, context, ct).ConfigureAwait(false);

		var delegatedRequest = request with
		{
			Alias = resolvedModel.Alias,
			PreferredTier = resolvedModel.Tier,
		};

		return new OverlayExecutionPlan(delegatedRequest, null);
	}

	internal static OverlayDecision DetermineDecision(InferenceRequest request, PolicyResult policyResult)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(policyResult);

		return policyResult.Action switch
		{
			PolicyAction.Allow => new OverlayDecision(request.Alias, null),
			PolicyAction.Deny => new OverlayDecision(
				request.Alias,
				string.IsNullOrWhiteSpace(policyResult.DenialReason)
					? "Inference request denied by policy."
					: policyResult.DenialReason),
			PolicyAction.Redirect when !string.IsNullOrWhiteSpace(policyResult.RedirectAlias) => new OverlayDecision(
				InferenceModelAlias.From(policyResult.RedirectAlias),
				null),
			PolicyAction.Redirect => throw new InvalidOperationException("Policy redirect requires a redirect alias."),
			_ => throw new InvalidOperationException($"Unsupported policy action '{policyResult.Action}'."),
		};
	}

	private static string? ExtractTenantId(IReadOnlyDictionary<string, string>? metadata)
	{
		if (metadata is null)
		{
			return null;
		}

		return metadata.TryGetValue("tenantId", out var tenantId)
			? tenantId
			: null;
	}

	public sealed record OverlayDecision(InferenceModelAlias AliasToResolve, string? DenialReason);

	public sealed record OverlayExecutionPlan(InferenceRequest Request, string? DenialReason);
}
