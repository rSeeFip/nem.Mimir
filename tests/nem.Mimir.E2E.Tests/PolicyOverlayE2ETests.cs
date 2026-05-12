using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using nem.Contracts.Identity;
using nem.Contracts.Inference;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Llm;
using nem.Mimir.Infrastructure.Inference;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.E2E.Tests;

public sealed class PolicyOverlayE2ETests
{
    private static IModelResolution BuildPassthroughModelResolution()
    {
        var modelResolution = Substitute.For<IModelResolution>();
        modelResolution
            .ResolveAsync(Arg.Any<InferenceModelAlias>(), Arg.Any<PolicyContext>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var alias = ci.Arg<InferenceModelAlias>();
                return Task.FromResult(new ResolvedModel(
                    alias,
                    InferenceProviderId.From(Guid.NewGuid()),
                    alias.Value,
                    "LiteLLM",
                    ModelTier.Standard,
                    131072));
            });
        return modelResolution;
    }

    // ─── Allow path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Policy_Allow_ExecutionPlan_HasNoDenialReason()
    {
        var allowPolicy = Substitute.For<IInferencePolicy>();
        allowPolicy
            .EvaluateAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PolicyResult(PolicyAction.Allow)));

        var overlay = new PolicyIntentOverlayService(
            Substitute.For<IInferenceGateway>(),
            allowPolicy,
            BuildPassthroughModelResolution());

        var request = new InferenceRequest(
            Alias: InferenceModelAlias.Standard,
            Messages: [new InferenceMessage("user", "Hello from E2E")],
            CorrelationId: CorrelationId.New(),
            Stream: false);

        var plan = await overlay.CreateExecutionPlanAsync(request, CancellationToken.None);

        plan.DenialReason.ShouldBeNull();
        plan.Request.ShouldNotBeNull();
    }

    // ─── Deny path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Policy_Deny_ExecutionPlan_HasDenialReason()
    {
        var denyPolicy = Substitute.For<IInferencePolicy>();
        denyPolicy
            .EvaluateAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PolicyResult(PolicyAction.Deny, DenialReason: "Tenant not authorised.")));

        var overlay = new PolicyIntentOverlayService(
            Substitute.For<IInferenceGateway>(),
            denyPolicy,
            BuildPassthroughModelResolution());

        var request = new InferenceRequest(
            Alias: InferenceModelAlias.Standard,
            Messages: [new InferenceMessage("user", "Attempt denied request")],
            CorrelationId: CorrelationId.New(),
            Stream: false);

        var plan = await overlay.CreateExecutionPlanAsync(request, CancellationToken.None);

        plan.DenialReason.ShouldNotBeNullOrWhiteSpace();
        plan.DenialReason.ShouldContain("Tenant not authorised.");
    }

    // ─── Redirect path ───────────────────────────────────────────────────────

    [Fact]
    public async Task Policy_Redirect_ExecutionPlan_SelectsDifferentModelAlias()
    {
        var redirectPolicy = Substitute.For<IInferencePolicy>();
        redirectPolicy
            .EvaluateAsync(Arg.Any<InferenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PolicyResult(PolicyAction.Redirect, RedirectAlias: "fast")));

        var overlay = new PolicyIntentOverlayService(
            Substitute.For<IInferenceGateway>(),
            redirectPolicy,
            BuildPassthroughModelResolution());

        var request = new InferenceRequest(
            Alias: InferenceModelAlias.Standard,
            Messages: [new InferenceMessage("user", "Redirect me to fast model")],
            CorrelationId: CorrelationId.New(),
            Stream: false);

        var plan = await overlay.CreateExecutionPlanAsync(request, CancellationToken.None);

        plan.DenialReason.ShouldBeNull();
        plan.Request.Alias.Value.ShouldBe("fast");
    }

    // ─── DI registration ─────────────────────────────────────────────────────

    [Fact]
    public void PolicyIntentOverlayService_CanBeConstructed_WithRealDependencies()
    {
        var services = new ServiceCollection();
        services.Configure<InferencePolicyOptions>(opts => opts.Action = PolicyAction.Allow);
        services.AddScoped<IInferencePolicy, AllowAllInferencePolicy>();
        services.AddScoped<IModelResolution>(_ => BuildPassthroughModelResolution());
        services.AddScoped<IInferenceGateway>(_ => Substitute.For<IInferenceGateway>());
        services.AddScoped<PolicyIntentOverlayService>();
        var sp = services.BuildServiceProvider();

        var overlay = sp.GetService<PolicyIntentOverlayService>();

        overlay.ShouldNotBeNull();
    }
}
