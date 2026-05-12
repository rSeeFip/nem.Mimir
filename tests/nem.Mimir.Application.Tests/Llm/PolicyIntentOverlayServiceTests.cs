using nem.Contracts.Identity;
using nem.Contracts.Inference;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Llm;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Llm;

public sealed class PolicyIntentOverlayServiceTests
{
	private readonly IInferenceGateway _innerGateway = Substitute.For<IInferenceGateway>();
	private readonly IInferencePolicy _defaultPolicy = Substitute.For<IInferencePolicy>();
	private readonly IModelResolution _modelResolution = Substitute.For<IModelResolution>();

	[Fact]
	public async Task InferWithPolicyAsync_Allow_ResolvesModelAndDelegatesUsingResolvedTier()
	{
		var request = CreateRequest(InferenceModelAlias.Fast);
		var policy = Substitute.For<IInferencePolicy>();
		var resolvedModel = new ResolvedModel(
			request.Alias,
			InferenceProviderId.New(),
			"gpt-4o",
			"OpenAI",
			ModelTier.Premium,
			128000);
		var expectedResponse = CreateResponse(request.Alias, ModelTier.Premium, "allowed");

		policy.EvaluateAsync(request, Arg.Any<CancellationToken>())
			.Returns(new PolicyResult(PolicyAction.Allow));
		_modelResolution.ResolveAsync(
			request.Alias,
			Arg.Is<PolicyContext>(context =>
				context.TenantId == "tenant-01"
				&& context.Metadata == request.Metadata),
			Arg.Any<CancellationToken>())
			.Returns(resolvedModel);
		_innerGateway.SendAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
			.Returns(expectedResponse);

		var sut = CreateSut();

		var result = await sut.InferWithPolicyAsync(request, policy, CancellationToken.None);

		result.ShouldBe(expectedResponse);
		await _modelResolution.Received(1).ResolveAsync(
			request.Alias,
			Arg.Is<PolicyContext>(context =>
				context.TenantId == "tenant-01"
				&& context.Metadata == request.Metadata),
			Arg.Any<CancellationToken>());
		await _innerGateway.Received(1).SendAsync(
			Arg.Is<InferenceRequest>(delegated =>
				delegated.Alias == request.Alias
				&& delegated.PreferredTier == ModelTier.Premium
				&& delegated.Metadata == request.Metadata),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task InferWithPolicyAsync_Deny_ThrowsAndDoesNotCallInnerGateway()
	{
		var request = CreateRequest(InferenceModelAlias.Standard);
		var policy = Substitute.For<IInferencePolicy>();

		policy.EvaluateAsync(request, Arg.Any<CancellationToken>())
			.Returns(new PolicyResult(PolicyAction.Deny, DenialReason: "policy denied"));

		var sut = CreateSut();

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.InferWithPolicyAsync(request, policy, CancellationToken.None));

		exception.Message.ShouldContain("policy denied");
		await _modelResolution.DidNotReceive().ResolveAsync(
			Arg.Any<InferenceModelAlias>(),
			Arg.Any<PolicyContext>(),
			Arg.Any<CancellationToken>());
		await _innerGateway.DidNotReceive().SendAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task InferWithPolicyAsync_Redirect_UsesRedirectAliasForDelegatedRequest()
	{
		var request = CreateRequest(InferenceModelAlias.Fast);
		var policy = Substitute.For<IInferencePolicy>();
		var redirectAlias = InferenceModelAlias.Premium;
		var resolvedModel = new ResolvedModel(
			redirectAlias,
			InferenceProviderId.New(),
			"claude-sonnet-4-20250514",
			"Anthropic",
			ModelTier.Premium,
			200000);
		var expectedResponse = CreateResponse(redirectAlias, ModelTier.Premium, "redirected");

		policy.EvaluateAsync(request, Arg.Any<CancellationToken>())
			.Returns(new PolicyResult(PolicyAction.Redirect, redirectAlias.Value));
		_modelResolution.ResolveAsync(redirectAlias, Arg.Any<PolicyContext>(), Arg.Any<CancellationToken>())
			.Returns(resolvedModel);
		_innerGateway.SendAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
			.Returns(expectedResponse);

		var sut = CreateSut();

		var result = await sut.InferWithPolicyAsync(request, policy, CancellationToken.None);

		result.ShouldBe(expectedResponse);
		await _modelResolution.Received(1).ResolveAsync(
			redirectAlias,
			Arg.Any<PolicyContext>(),
			Arg.Any<CancellationToken>());
		await _innerGateway.Received(1).SendAsync(
			Arg.Is<InferenceRequest>(delegated =>
				delegated.Alias == redirectAlias
				&& delegated.PreferredTier == ModelTier.Premium),
			Arg.Any<CancellationToken>());
	}

	private PolicyIntentOverlayService CreateSut()
	{
		return new PolicyIntentOverlayService(_innerGateway, _defaultPolicy, _modelResolution);
	}

	private static InferenceRequest CreateRequest(InferenceModelAlias alias)
	{
		return new InferenceRequest(
			alias,
			new[] { new InferenceMessage("user", "hello") },
			CorrelationId.New(),
			Metadata: new Dictionary<string, string>
			{
				["tenantId"] = "tenant-01"
			});
	}

	private static InferenceResponse CreateResponse(InferenceModelAlias alias, ModelTier tier, string content)
	{
		return new InferenceResponse(
			content,
			alias,
			InferenceProviderId.New(),
			"resolved-model",
			tier,
			CorrelationId.New(),
			0,
			0,
			0,
			"stop");
	}
}
