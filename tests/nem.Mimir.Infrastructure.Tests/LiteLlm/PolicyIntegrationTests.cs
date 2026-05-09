namespace nem.Mimir.Infrastructure.Tests.LiteLlm;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using nem.Contracts.Identity;
using nem.Contracts.Inference;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Llm;
using nem.Mimir.Infrastructure.Inference;
using nem.Mimir.Infrastructure.LiteLlm;
using NSubstitute;
using Shouldly;

public sealed class PolicyIntegrationTests
{
    [Fact]
    public async Task SendMessageAsync_EvaluatesPolicyBeforeCallingInnerLlm()
    {
        var inner = Substitute.For<ILlmService>();
        var policy = Substitute.For<IInferencePolicy>();
        var modelResolution = Substitute.For<IModelResolution>();
        var decorator = CreateDecorator(inner, policy, modelResolution);
        var messages = CreateMessages();
        var response = new LlmResponse("allowed", LlmModels.Primary, 1, 1, 2, "stop");

        policy.EvaluateAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                inner.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
                return Task.FromResult(new PolicyResult(PolicyAction.Allow));
            });
        modelResolution.ResolveAsync(Arg.Any<InferenceModelAlias>(), Arg.Any<PolicyContext>(), Arg.Any<CancellationToken>())
            .Returns(CreateResolvedModel(InferenceModelAlias.Standard, ModelTier.Standard));
        inner.SendMessageAsync(LlmModels.Primary, messages, Arg.Any<CancellationToken>())
            .Returns(response);

        var result = await decorator.SendMessageAsync(LlmModels.Primary, messages, CancellationToken.None);

        result.ShouldBe(response);
        await policy.Received(1).EvaluateAsync(
            Arg.Is<InferenceRequest>(request =>
                request.Alias == InferenceModelAlias.Standard &&
                request.Messages.Count == messages.Count),
            Arg.Any<CancellationToken>());
        await inner.Received(1).SendMessageAsync(LlmModels.Primary, messages, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_Deny_ReturnsErrorResponseAndDoesNotCallInnerLlm()
    {
        var inner = Substitute.For<ILlmService>();
        var policy = Substitute.For<IInferencePolicy>();
        var modelResolution = Substitute.For<IModelResolution>();
        var decorator = CreateDecorator(inner, policy, modelResolution);

        policy.EvaluateAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PolicyResult(PolicyAction.Deny, DenialReason: "policy denied"));

        var result = await decorator.SendMessageAsync(LlmModels.Primary, CreateMessages(), CancellationToken.None);

        result.Content.ShouldBe("policy denied");
        result.Model.ShouldBe(LlmModels.Primary);
        result.FinishReason.ShouldBe("policy_denied");
        await modelResolution.DidNotReceive().ResolveAsync(
            Arg.Any<InferenceModelAlias>(),
            Arg.Any<PolicyContext>(),
            Arg.Any<CancellationToken>());
        await inner.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
    }

    [Fact]
    public async Task SendMessageAsync_Redirect_OverridesModelBeforeCallingInnerLlm()
    {
        var inner = Substitute.For<ILlmService>();
        var policy = Substitute.For<IInferencePolicy>();
        var modelResolution = Substitute.For<IModelResolution>();
        var decorator = CreateDecorator(inner, policy, modelResolution);
        var messages = CreateMessages();
        var response = new LlmResponse("redirected", LlmModels.Coding, 1, 1, 2, "stop");

        policy.EvaluateAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PolicyResult(PolicyAction.Redirect, RedirectAlias: InferenceModelAlias.Coding.Value));
        modelResolution.ResolveAsync(
                Arg.Is<InferenceModelAlias>(alias => alias == InferenceModelAlias.Coding),
                Arg.Any<PolicyContext>(),
                Arg.Any<CancellationToken>())
            .Returns(CreateResolvedModel(InferenceModelAlias.Coding, ModelTier.Premium));
        inner.SendMessageAsync(LlmModels.Coding, messages, Arg.Any<CancellationToken>())
            .Returns(response);

        var result = await decorator.SendMessageAsync(LlmModels.Primary, messages, CancellationToken.None);

        result.ShouldBe(response);
        await inner.Received(1).SendMessageAsync(LlmModels.Coding, messages, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_NoPolicyConfigured_PassthroughUsesOriginalModel()
    {
        var inner = Substitute.For<ILlmService>();
        var policy = Substitute.For<IInferencePolicy>();
        var modelResolution = Substitute.For<IModelResolution>();
        var decorator = CreateDecorator(inner, policy, modelResolution);
        var messages = CreateMessages();
        var response = new LlmResponse("passthrough", LlmModels.Fast, 1, 1, 2, "stop");

        policy.EvaluateAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PolicyResult(PolicyAction.Allow));
        modelResolution.ResolveAsync(Arg.Any<InferenceModelAlias>(), Arg.Any<PolicyContext>(), Arg.Any<CancellationToken>())
            .Returns(CreateResolvedModel(InferenceModelAlias.Fast, ModelTier.Standard));
        inner.SendMessageAsync(LlmModels.Fast, messages, Arg.Any<CancellationToken>())
            .Returns(response);

        var result = await decorator.SendMessageAsync(LlmModels.Fast, messages, CancellationToken.None);

        result.ShouldBe(response);
        await inner.Received(1).SendMessageAsync(LlmModels.Fast, messages, Arg.Any<CancellationToken>());
    }

    private static PolicyLlmServiceDecorator CreateDecorator(
        ILlmService inner,
        IInferencePolicy policy,
        IModelResolution modelResolution)
    {
        var overlay = new PolicyIntentOverlayService(
            Substitute.For<IInferenceGateway>(),
            policy,
            modelResolution);

        return new PolicyLlmServiceDecorator(inner, overlay);
    }

    private static IReadOnlyList<LlmMessage> CreateMessages()
        => new[] { new LlmMessage("user", "hello") };

    private static ResolvedModel CreateResolvedModel(InferenceModelAlias alias, ModelTier tier)
        => new(
            alias,
            InferenceProviderId.From(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            alias.Value,
            "LiteLLM",
            tier,
            131072);
}
