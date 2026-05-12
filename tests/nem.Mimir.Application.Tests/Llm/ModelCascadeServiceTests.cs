using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Llm;
using nem.Contracts.TokenOptimization;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Llm;

public sealed class ModelCascadeServiceTests
{
    private readonly ILogger<ModelCascadeService> _logger = Substitute.For<ILogger<ModelCascadeService>>();

    #region SelectModelAsync — Tier Selection per Task Type

    [Fact]
    public async Task SelectModelAsync_SimpleTaskLowTokens_ReturnsFastTier()
    {
        var sut = CreateSut();
        var context = new ModelSelectionContext("summarization", 500, 200);

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        result.Tier.ShouldBe(ModelTier.Fast);
    }

    [Fact]
    public async Task SelectModelAsync_DefaultTask_ReturnsStandardTier()
    {
        var sut = CreateSut();
        var context = new ModelSelectionContext("generation", 1500, 500);

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        result.Tier.ShouldBe(ModelTier.Standard);
    }

    [Fact]
    public async Task SelectModelAsync_ReasoningRequired_ReturnsPremiumTier()
    {
        var sut = CreateSut();
        var context = new ModelSelectionContext("analysis", 2000, 1000, RequiresReasoning: true);

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        result.Tier.ShouldBe(ModelTier.Premium);
    }

    [Fact]
    public async Task SelectModelAsync_ExpertMappedTaskType_ReturnsExpertTier()
    {
        var options = new ModelCascadeOptions
        {
            TaskTypeComplexityMap = new Dictionary<string, ModelTier>(StringComparer.OrdinalIgnoreCase)
            {
                ["research"] = ModelTier.Expert
            }
        };
        var sut = CreateSut(options);
        var context = new ModelSelectionContext("research", 5000, 2000);

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        result.Tier.ShouldBe(ModelTier.Expert);
    }

    [Fact]
    public async Task SelectModelAsync_HighTokenCount_EscalatesToStandard()
    {
        var sut = CreateSut();
        // classification is "simple" but >= 1000 tokens should escalate
        var context = new ModelSelectionContext("classification", 2000, 500);

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        result.Tier.ShouldBe(ModelTier.Standard);
    }

    [Fact]
    public async Task SelectModelAsync_TaskTypeComplexityMap_OverridesDefault()
    {
        var options = new ModelCascadeOptions
        {
            TaskTypeComplexityMap = new Dictionary<string, ModelTier>(StringComparer.OrdinalIgnoreCase)
            {
                ["generation"] = ModelTier.Premium
            }
        };
        var sut = CreateSut(options);
        var context = new ModelSelectionContext("generation", 500, 200);

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        result.Tier.ShouldBe(ModelTier.Premium);
    }

    #endregion

    #region SelectModelAsync — Fallback Behavior

    [Fact]
    public async Task SelectModelAsync_NoModelForTier_FallsBackToNextTierUp()
    {
        var options = new ModelCascadeOptions { EnableFallback = true, MaxFallbackAttempts = 2 };
        var sut = CreateSut(options);

        // Remove all Fast tier models so fallback triggers
        // Register only Standard and higher
        sut.RegisterModel(new ModelConfig("custom-standard", "Custom", ModelTier.Standard, 0.002m, 0.008m, 64000));

        // Clear default Fast model by re-registering with different tier — actually we need to force context to pick a missing tier
        // Use a mapped task type to force a specific tier that has no model
        var optionsWithMap = new ModelCascadeOptions
        {
            EnableFallback = true,
            MaxFallbackAttempts = 2,
            TaskTypeComplexityMap = new Dictionary<string, ModelTier>(StringComparer.OrdinalIgnoreCase)
            {
                ["special"] = ModelTier.Fast
            }
        };
        var sut2 = new ModelCascadeService(optionsWithMap, _logger);

        // Unregister the default Fast model by clearing and only registering Standard+
        // Since we can't remove, let's work around: the service has defaults registered.
        // The default gpt-4o-mini is Fast, so this test will get that. Let's test differently.

        // Test: Create service with no defaults, only register Standard
        var sut3 = CreateSutWithoutDefaults(new ModelCascadeOptions { EnableFallback = true, MaxFallbackAttempts = 2 });
        sut3.RegisterModel(new ModelConfig("standard-model", "TestProvider", ModelTier.Standard, 0.002m, 0.008m, 64000));

        var context = new ModelSelectionContext("summarization", 100, 50);

        var result = await sut3.SelectModelAsync(context, CancellationToken.None);

        // Fast tier has no model, should fallback to Standard
        result.Tier.ShouldBe(ModelTier.Standard);
        result.ModelId.ShouldBe("standard-model");
    }

    [Fact]
    public async Task SelectModelAsync_FallbackDisabled_ReturnsDefaultFallback()
    {
        var options = new ModelCascadeOptions { EnableFallback = false };
        var sut = CreateSutWithoutDefaults(options);

        // Only register a Premium model — no Standard or Fast
        sut.RegisterModel(new ModelConfig("premium-only", "TestProvider", ModelTier.Premium, 0.003m, 0.015m, 200000));

        var context = new ModelSelectionContext("summarization", 100, 50);

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        // With fallback disabled, should return graceful default (Premium is the only registered model)
        result.ShouldNotBeNull();
        result.ModelId.ShouldBe("premium-only");
    }

    [Fact]
    public async Task SelectModelAsync_FallbackExceedsMaxAttempts_ReturnsDefaultFallback()
    {
        var options = new ModelCascadeOptions { EnableFallback = true, MaxFallbackAttempts = 1 };
        var sut = CreateSutWithoutDefaults(options);

        // Only register Expert model, request Fast — needs 3 hops (Fast→Standard→Premium→Expert) but max is 1
        sut.RegisterModel(new ModelConfig("expert-only", "TestProvider", ModelTier.Expert, 0.015m, 0.075m, 200000));

        var context = new ModelSelectionContext("summarization", 100, 50);

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        // Fallback limited to 1 attempt (Fast→Standard only), Expert not reachable
        // Graceful degradation returns any registered model
        result.ShouldNotBeNull();
        result.ModelId.ShouldBe("expert-only");
    }

    #endregion

    #region SelectModelAsync — Preferred Provider Routing

    [Fact]
    public async Task SelectModelAsync_PreferredProvider_SelectsMatchingProvider()
    {
        var sut = CreateSut();

        // Register an Anthropic Standard model
        sut.RegisterModel(new ModelConfig("claude-haiku", "Anthropic", ModelTier.Standard, 0.001m, 0.005m, 200000));

        var context = new ModelSelectionContext("generation", 1500, 500, PreferredProvider: "Anthropic");

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        result.Provider.ShouldBe("Anthropic");
        result.Tier.ShouldBe(ModelTier.Standard);
    }

    [Fact]
    public async Task SelectModelAsync_PreferredProvider_FallsBackWhenNoMatch()
    {
        var sut = CreateSut();

        var context = new ModelSelectionContext("generation", 1500, 500, PreferredProvider: "NonExistentProvider");

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        // Should still return a model even though preferred provider doesn't exist
        result.ShouldNotBeNull();
        result.Tier.ShouldBe(ModelTier.Standard);
    }

    [Fact]
    public async Task SelectModelAsync_PreferredProvider_PicksLowestCostFromProvider()
    {
        var sut = CreateSut();

        sut.RegisterModel(new ModelConfig("anthropic-cheap", "Anthropic", ModelTier.Standard, 0.0005m, 0.002m, 100000));
        sut.RegisterModel(new ModelConfig("anthropic-expensive", "Anthropic", ModelTier.Standard, 0.005m, 0.020m, 200000));

        var context = new ModelSelectionContext("generation", 1500, 500, PreferredProvider: "Anthropic");

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        result.ModelId.ShouldBe("anthropic-cheap");
    }

    #endregion

    #region RegisterModel

    [Fact]
    public async Task RegisterModel_StoresConfig()
    {
        var sut = CreateSut();
        var config = new ModelConfig("test-model", "TestProvider", ModelTier.Standard, 0.001m, 0.002m, 32000);

        sut.RegisterModel(config);

        // Verify by selecting it with preferred provider
        var context = new ModelSelectionContext("generation", 1500, 500, PreferredProvider: "TestProvider");
        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        result.ModelId.ShouldBe("test-model");
    }

    [Fact]
    public async Task RegisterModel_OverwritesSameModelId()
    {
        var sut = CreateSutWithoutDefaults();
        var original = new ModelConfig("test-model", "ProviderA", ModelTier.Standard, 0.001m, 0.002m, 32000);
        var updated = new ModelConfig("test-model", "ProviderB", ModelTier.Standard, 0.0005m, 0.001m, 64000);

        sut.RegisterModel(original);
        sut.RegisterModel(updated);

        var context = new ModelSelectionContext("generation", 1500, 500);
        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        // The model should have been overwritten — provider should be ProviderB now
        result.Provider.ShouldBe("ProviderB");
        result.ModelId.ShouldBe("test-model");
    }

    [Fact]
    public void RegisterModel_ThrowsOnNull()
    {
        var sut = CreateSut();

        Should.Throw<ArgumentNullException>(() => sut.RegisterModel(null!));
    }

    [Fact]
    public void RegisterModel_ThrowsOnEmptyModelId()
    {
        var sut = CreateSut();
        var config = new ModelConfig("", "TestProvider", ModelTier.Standard, 0.001m, 0.002m, 32000);

        Should.Throw<ArgumentException>(() => sut.RegisterModel(config));
    }

    #endregion

    #region GetTiers

    [Fact]
    public void GetTiers_ReturnsAllTiersInOrder()
    {
        var sut = CreateSut();

        var tiers = sut.GetTiers();

        tiers.Count.ShouldBe(4);
        tiers[0].ShouldBe(ModelTier.Fast);
        tiers[1].ShouldBe(ModelTier.Standard);
        tiers[2].ShouldBe(ModelTier.Premium);
        tiers[3].ShouldBe(ModelTier.Expert);
    }

    #endregion

    #region Default Behavior

    [Fact]
    public async Task SelectModelAsync_ThrowsOnNullContext()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.SelectModelAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task SelectModelAsync_DefaultOptions_RegistersDefaultModels()
    {
        var sut = CreateSut();

        // Verify defaults are registered by selecting each tier
        var fast = await sut.SelectModelAsync(
            new ModelSelectionContext("summarization", 100, 50), CancellationToken.None);
        fast.Tier.ShouldBe(ModelTier.Fast);

        var standard = await sut.SelectModelAsync(
            new ModelSelectionContext("generation", 1500, 500), CancellationToken.None);
        standard.Tier.ShouldBe(ModelTier.Standard);

        var premium = await sut.SelectModelAsync(
            new ModelSelectionContext("analysis", 2000, 1000, RequiresReasoning: true), CancellationToken.None);
        premium.Tier.ShouldBe(ModelTier.Premium);
    }

    [Fact]
    public async Task SelectModelAsync_NoModelsRegistered_ReturnssyntheticFallback()
    {
        var sut = CreateSutWithoutDefaults(new ModelCascadeOptions { EnableFallback = false });

        var context = new ModelSelectionContext("generation", 1500, 500);

        var result = await sut.SelectModelAsync(context, CancellationToken.None);

        // Should return synthetic fallback
        result.ShouldNotBeNull();
        result.Tier.ShouldBe(ModelTier.Standard);
        result.ModelId.ShouldBe("gpt-4o");
    }

    [Fact]
    public async Task SelectModelAsync_CancellationRequested_Throws()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.SelectModelAsync(
                new ModelSelectionContext("generation", 500, 200), cts.Token));
    }

    [Fact]
    public void Constructor_ThrowsOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new ModelCascadeService(null!, _logger));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        Should.Throw<ArgumentNullException>(() => new ModelCascadeService(new ModelCascadeOptions(), null!));
    }

    #endregion

    #region Options Defaults

    [Fact]
    public void ModelCascadeOptions_HasExpectedDefaults()
    {
        var options = new ModelCascadeOptions();

        options.DefaultTier.ShouldBe(ModelTier.Standard);
        options.EnableFallback.ShouldBeTrue();
        options.MaxFallbackAttempts.ShouldBe(2);
        options.TaskTypeComplexityMap.ShouldNotBeNull();
        options.TaskTypeComplexityMap.ShouldBeEmpty();
    }

    #endregion

    #region Helpers

    private ModelCascadeService CreateSut(ModelCascadeOptions? options = null)
    {
        return new ModelCascadeService(options ?? new ModelCascadeOptions(), _logger);
    }

    /// <summary>
    /// Creates a service instance that does NOT register default models, for precise test control.
    /// Uses reflection to clear the internal model dictionary after construction.
    /// </summary>
    private ModelCascadeService CreateSutWithoutDefaults(ModelCascadeOptions? options = null)
    {
        var sut = new ModelCascadeService(options ?? new ModelCascadeOptions(), _logger);

        // Clear default models via reflection for test isolation
        var field = typeof(ModelCascadeService).GetField("_models",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dict = field?.GetValue(sut) as System.Collections.Concurrent.ConcurrentDictionary<string, ModelConfig>;
        dict?.Clear();

        return sut;
    }

    #endregion
}
