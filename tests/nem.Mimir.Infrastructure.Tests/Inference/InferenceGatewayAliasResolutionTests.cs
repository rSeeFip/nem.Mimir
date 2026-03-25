namespace nem.Mimir.Infrastructure.Tests.Inference;

using Microsoft.Extensions.Logging;
using nem.Contracts.Identity;
using nem.Contracts.Inference;
using nem.Contracts.TokenOptimization;
using nem.MCP.Infrastructure.Inference;
using NSubstitute;
using Shouldly;

/// <summary>
/// Tests for <see cref="InferenceGatewayService"/> alias resolution.
/// Validates that logical <see cref="InferenceModelAlias"/> values resolve to
/// the correct provider+model pairs through <see cref="IModelCascade"/>.
/// </summary>
public sealed class InferenceGatewayAliasResolutionTests
{
    private readonly IModelCascade _modelCascade;
    private readonly ILogger<InferenceGatewayService> _logger;
    private readonly InferenceGatewayService _gateway;

    public InferenceGatewayAliasResolutionTests()
    {
        _modelCascade = Substitute.For<IModelCascade>();
        _logger = Substitute.For<ILogger<InferenceGatewayService>>();
        _gateway = new InferenceGatewayService(_modelCascade, _logger);
    }

    // ─── ResolveAliasToTier Static Tests ──────────────────────────

    [Theory]
    [InlineData("fast", ModelTier.Fast)]
    [InlineData("standard", ModelTier.Standard)]
    [InlineData("premium", ModelTier.Premium)]
    [InlineData("expert", ModelTier.Expert)]
    [InlineData("coding", ModelTier.Premium)]
    [InlineData("embedding", ModelTier.Fast)]
    public void ResolveAliasToTier_WellKnownAlias_ReturnsMappedTier(string alias, ModelTier expected)
    {
        var result = InferenceGatewayService.ResolveAliasToTier(InferenceModelAlias.From(alias));
        result.ShouldBe(expected);
    }

    [Fact]
    public void ResolveAliasToTier_UnknownAlias_FallsBackToStandard()
    {
        var result = InferenceGatewayService.ResolveAliasToTier(InferenceModelAlias.From("unknown-model"));
        result.ShouldBe(ModelTier.Standard);
    }

    [Fact]
    public void ResolveAliasToTier_EmptyAlias_FallsBackToStandard()
    {
        var result = InferenceGatewayService.ResolveAliasToTier(InferenceModelAlias.Empty);
        result.ShouldBe(ModelTier.Standard);
    }

    [Theory]
    [InlineData("FAST", ModelTier.Fast)]
    [InlineData("Standard", ModelTier.Standard)]
    [InlineData("PREMIUM", ModelTier.Premium)]
    public void ResolveAliasToTier_CaseInsensitive_ResolvesCorrectly(string alias, ModelTier expected)
    {
        var result = InferenceGatewayService.ResolveAliasToTier(InferenceModelAlias.From(alias));
        result.ShouldBe(expected);
    }

    // ─── ResolveModelAsync Integration Tests ──────────────────────

    [Fact]
    public async Task ResolveModelAsync_FastAlias_DelegatesToCascadeAndReturnsResolvedModel()
    {
        // Arrange
        var alias = InferenceModelAlias.Fast;
        var expectedConfig = new ModelConfig("gpt-4o-mini", "OpenAI", ModelTier.Fast, 0.00015m, 0.0006m, 128000);

        _modelCascade
            .SelectModelAsync(Arg.Any<ModelSelectionContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedConfig));

        // Act
        var result = await _gateway.ResolveModelAsync(alias, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Alias.ShouldBe(alias);
        result.ModelId.ShouldBe("gpt-4o-mini");
        result.Provider.ShouldBe("OpenAI");
        result.Tier.ShouldBe(ModelTier.Fast);
        result.MaxContextWindow.ShouldBe(128000);
        result.ProviderId.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveModelAsync_PremiumAlias_ResolvesCorrectly()
    {
        // Arrange
        var alias = InferenceModelAlias.Premium;
        var expectedConfig = new ModelConfig("claude-sonnet-4-20250514", "Anthropic", ModelTier.Premium, 0.003m, 0.015m, 200000);

        _modelCascade
            .SelectModelAsync(Arg.Any<ModelSelectionContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedConfig));

        // Act
        var result = await _gateway.ResolveModelAsync(alias, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Alias.ShouldBe(alias);
        result.ModelId.ShouldBe("claude-sonnet-4-20250514");
        result.Provider.ShouldBe("Anthropic");
        result.Tier.ShouldBe(ModelTier.Premium);
        result.ProviderId.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveModelAsync_EmptyAlias_ReturnsNull()
    {
        var result = await _gateway.ResolveModelAsync(InferenceModelAlias.Empty, TestContext.Current.CancellationToken);
        result.ShouldBeNull();

        // Verify cascade was never called for empty alias.
        await _modelCascade.DidNotReceive()
            .SelectModelAsync(Arg.Any<ModelSelectionContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveModelAsync_SameProvider_ReturnsStableProviderId()
    {
        // Arrange — two different aliases that both resolve to OpenAI models.
        var fastConfig = new ModelConfig("gpt-4o-mini", "OpenAI", ModelTier.Fast, 0.00015m, 0.0006m, 128000);
        var standardConfig = new ModelConfig("gpt-4o", "OpenAI", ModelTier.Standard, 0.0025m, 0.010m, 128000);

        _modelCascade
            .SelectModelAsync(Arg.Any<ModelSelectionContext>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(fastConfig),
                Task.FromResult(standardConfig));

        // Act
        var result1 = await _gateway.ResolveModelAsync(InferenceModelAlias.Fast, TestContext.Current.CancellationToken);
        var result2 = await _gateway.ResolveModelAsync(InferenceModelAlias.Standard, TestContext.Current.CancellationToken);

        // Assert — same provider → same opaque ID.
        result1.ShouldNotBeNull();
        result2.ShouldNotBeNull();
        result1.ProviderId.ShouldBe(result2.ProviderId);
    }

    [Fact]
    public async Task ResolveModelAsync_DifferentProviders_ReturnsDifferentProviderIds()
    {
        // Arrange
        var openAiConfig = new ModelConfig("gpt-4o", "OpenAI", ModelTier.Standard, 0.0025m, 0.010m, 128000);
        var anthropicConfig = new ModelConfig("claude-sonnet-4-20250514", "Anthropic", ModelTier.Premium, 0.003m, 0.015m, 200000);

        _modelCascade
            .SelectModelAsync(Arg.Any<ModelSelectionContext>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(openAiConfig),
                Task.FromResult(anthropicConfig));

        // Act
        var result1 = await _gateway.ResolveModelAsync(InferenceModelAlias.Standard, TestContext.Current.CancellationToken);
        var result2 = await _gateway.ResolveModelAsync(InferenceModelAlias.Premium, TestContext.Current.CancellationToken);

        // Assert — different providers → different opaque IDs.
        result1.ShouldNotBeNull();
        result2.ShouldNotBeNull();
        result1.ProviderId.ShouldNotBe(result2.ProviderId);
    }

    // ─── SendAsync Tests ──────────────────────────────────────────

    [Fact]
    public async Task SendAsync_ValidRequest_ResolvesModelAndReturnsResponse()
    {
        // Arrange
        var request = new InferenceRequest(
            Alias: InferenceModelAlias.Standard,
            Messages: [new InferenceMessage("user", "Hello")],
            CorrelationId: CorrelationId.New());

        var config = new ModelConfig("gpt-4o", "OpenAI", ModelTier.Standard, 0.0025m, 0.010m, 128000);
        _modelCascade
            .SelectModelAsync(Arg.Any<ModelSelectionContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(config));

        // Act
        var response = await _gateway.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.ShouldNotBeNull();
        response.ResolvedAlias.ShouldBe(InferenceModelAlias.Standard);
        response.ResolvedModelId.ShouldBe("gpt-4o");
        response.CorrelationId.ShouldBe(request.CorrelationId);
        response.ResolvedProviderId.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WithPreferredTier_OverridesAliasResolution()
    {
        // Arrange — request "fast" alias but override to Expert tier.
        var request = new InferenceRequest(
            Alias: InferenceModelAlias.Fast,
            Messages: [new InferenceMessage("user", "Explain quantum computing")],
            CorrelationId: CorrelationId.New(),
            PreferredTier: ModelTier.Expert);

        var expertConfig = new ModelConfig("claude-opus-4-20250514", "Anthropic", ModelTier.Expert, 0.015m, 0.075m, 200000);
        _modelCascade
            .SelectModelAsync(
                Arg.Is<ModelSelectionContext>(c => c.RequiresReasoning),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expertConfig));

        // Act
        var response = await _gateway.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert — should have used the Expert tier, not Fast.
        response.ResolvedTier.ShouldBe(ModelTier.Expert);
        response.ResolvedModelId.ShouldBe("claude-opus-4-20250514");
    }

    // ─── StreamAsync Tests ────────────────────────────────────────

    [Fact]
    public async Task StreamAsync_ValidRequest_YieldsResolvedChunk()
    {
        // Arrange
        var request = new InferenceRequest(
            Alias: InferenceModelAlias.Standard,
            Messages: [new InferenceMessage("user", "Hi")],
            CorrelationId: CorrelationId.New(),
            Stream: true);

        var config = new ModelConfig("gpt-4o", "OpenAI", ModelTier.Standard, 0.0025m, 0.010m, 128000);
        _modelCascade
            .SelectModelAsync(Arg.Any<ModelSelectionContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(config));

        // Act
        var chunks = new List<InferenceStreamChunk>();
        await foreach (var chunk in _gateway.StreamAsync(request, TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.ShouldNotBeEmpty();
        chunks[0].ResolvedModelId.ShouldBe("gpt-4o");
    }

    // ─── InferenceModelAlias Value Object Tests ───────────────────

    [Fact]
    public void InferenceModelAlias_EqualityIsCaseInsensitive()
    {
        var lower = InferenceModelAlias.From("fast");
        var upper = InferenceModelAlias.From("FAST");
        var mixed = InferenceModelAlias.From("Fast");

        lower.ShouldBe(upper);
        lower.ShouldBe(mixed);
        lower.GetHashCode().ShouldBe(upper.GetHashCode());
    }

    [Fact]
    public void InferenceModelAlias_WellKnownAliases_AreNotEmpty()
    {
        InferenceModelAlias.Fast.IsEmpty.ShouldBeFalse();
        InferenceModelAlias.Standard.IsEmpty.ShouldBeFalse();
        InferenceModelAlias.Premium.IsEmpty.ShouldBeFalse();
        InferenceModelAlias.Expert.IsEmpty.ShouldBeFalse();
        InferenceModelAlias.Coding.IsEmpty.ShouldBeFalse();
        InferenceModelAlias.Embedding.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void InferenceModelAlias_Empty_IsEmpty()
    {
        InferenceModelAlias.Empty.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void InferenceProviderId_NewCreatesUniqueIds()
    {
        var id1 = InferenceProviderId.New();
        var id2 = InferenceProviderId.New();

        id1.ShouldNotBe(id2);
        id1.IsEmpty.ShouldBeFalse();
        id2.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void InferenceProviderId_RoundTrips()
    {
        var original = InferenceProviderId.New();
        var asGuid = (Guid)original;
        var roundTripped = (InferenceProviderId)asGuid;

        roundTripped.ShouldBe(original);
    }

    [Fact]
    public void InferenceModelAlias_ParseAndTryParse_Work()
    {
        var parsed = InferenceModelAlias.Parse("fast", null);
        parsed.Value.ShouldBe("fast");

        InferenceModelAlias.TryParse("premium", null, out var result).ShouldBeTrue();
        result.Value.ShouldBe("premium");

        InferenceModelAlias.TryParse(null, null, out var empty).ShouldBeFalse();
        empty.IsEmpty.ShouldBeTrue();
    }
}
