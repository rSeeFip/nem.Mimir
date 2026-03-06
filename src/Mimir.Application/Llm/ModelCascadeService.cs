using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using nem.Contracts.TokenOptimization;

namespace Mimir.Application.Llm;

/// <summary>
/// Provides intelligent model tier routing for cost optimization by selecting the most
/// appropriate model based on task context, token estimates, and registered model configurations.
/// </summary>
public sealed class ModelCascadeService : IModelCascade
{
    private readonly ConcurrentDictionary<string, ModelConfig> _models = new(StringComparer.Ordinal);
    private readonly ModelCascadeOptions _options;
    private readonly ILogger<ModelCascadeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelCascadeService"/> class
    /// and registers default model configurations for each tier.
    /// </summary>
    /// <param name="options">Model cascade configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public ModelCascadeService(ModelCascadeOptions options, ILogger<ModelCascadeService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _logger = logger;

        RegisterDefaultModels();
    }

    /// <inheritdoc />
    public Task<ModelConfig> SelectModelAsync(ModelSelectionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var requiredTier = DetermineRequiredTier(context);

            _logger.LogDebug(
                "Determined required tier {Tier} for task type '{TaskType}' (input={InputTokens}, reasoning={RequiresReasoning}).",
                requiredTier,
                context.TaskType,
                context.EstimatedInputTokens,
                context.RequiresReasoning);

            var selected = FindModelForTier(requiredTier, context.PreferredProvider);

            if (selected is not null)
            {
                _logger.LogDebug("Selected model {ModelId} (tier={Tier}, provider={Provider}).", selected.ModelId, selected.Tier, selected.Provider);
                return Task.FromResult(selected);
            }

            // Fallback: try higher tiers
            if (_options.EnableFallback)
            {
                selected = AttemptFallback(requiredTier, context.PreferredProvider);

                if (selected is not null)
                {
                    _logger.LogInformation(
                        "Fell back from tier {RequestedTier} to model {ModelId} (tier={ActualTier}).",
                        requiredTier,
                        selected.ModelId,
                        selected.Tier);
                    return Task.FromResult(selected);
                }
            }

            // Graceful degradation: return default Standard tier model
            var fallback = GetDefaultFallbackModel();

            _logger.LogWarning(
                "No model found for tier {Tier}; returning default fallback model {ModelId}.",
                requiredTier,
                fallback.ModelId);

            return Task.FromResult(fallback);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentNullException)
        {
            _logger.LogError(ex, "Error selecting model for task type '{TaskType}'; returning default fallback.", context.TaskType);
            return Task.FromResult(GetDefaultFallbackModel());
        }
    }

    /// <inheritdoc />
    public void RegisterModel(ModelConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.ModelId);

        _models[config.ModelId] = config;

        _logger.LogDebug(
            "Registered model {ModelId} (provider={Provider}, tier={Tier}, contextWindow={MaxContextWindow}).",
            config.ModelId,
            config.Provider,
            config.Tier,
            config.MaxContextWindow);
    }

    /// <inheritdoc />
    public IReadOnlyList<ModelTier> GetTiers()
    {
        return Enum.GetValues<ModelTier>().OrderBy(t => (int)t).ToList().AsReadOnly();
    }

    /// <summary>
    /// Determines the minimum required tier based on task context, including task type complexity,
    /// token estimates, and reasoning requirements.
    /// </summary>
    private ModelTier DetermineRequiredTier(ModelSelectionContext context)
    {
        var tier = _options.DefaultTier;

        // Check TaskTypeComplexityMap first
        if (!string.IsNullOrWhiteSpace(context.TaskType)
            && _options.TaskTypeComplexityMap.TryGetValue(context.TaskType, out var mappedTier))
        {
            tier = mappedTier;
        }

        // Token count escalation: large inputs need higher tiers
        if (context.EstimatedInputTokens >= 1000)
        {
            tier = MaxTier(tier, ModelTier.Standard);
        }

        // Reasoning requirement escalates to Premium
        if (context.RequiresReasoning)
        {
            tier = MaxTier(tier, ModelTier.Premium);
        }

        // Simple tasks with low token counts can use Fast tier
        // Only downgrade if no other signal pushed us higher
        if (tier == _options.DefaultTier
            && !context.RequiresReasoning
            && context.EstimatedInputTokens < 1000
            && IsSimpleTaskType(context.TaskType))
        {
            tier = ModelTier.Fast;
        }

        // Expert tier is never auto-selected; it requires explicit mapping
        // via TaskTypeComplexityMap. We cap at Premium for automatic selection.
        if (tier == ModelTier.Expert
            && !string.IsNullOrWhiteSpace(context.TaskType)
            && !_options.TaskTypeComplexityMap.ContainsKey(context.TaskType))
        {
            tier = ModelTier.Premium;
        }

        return tier;
    }

    /// <summary>
    /// Finds the lowest-cost model matching the requested tier, optionally preferring a specific provider.
    /// </summary>
    private ModelConfig? FindModelForTier(ModelTier tier, string? preferredProvider)
    {
        var candidates = _models.Values
            .Where(m => m.Tier == tier)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        // Prefer provider models if specified
        if (!string.IsNullOrWhiteSpace(preferredProvider))
        {
            var providerModels = candidates
                .Where(m => string.Equals(m.Provider, preferredProvider, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (providerModels.Count > 0)
            {
                candidates = providerModels;
            }
        }

        // Pick lowest-cost model (by combined input+output cost as proxy)
        return candidates
            .OrderBy(m => m.CostPerInputToken + m.CostPerOutputToken)
            .First();
    }

    /// <summary>
    /// Attempts to find a model by cascading up to higher tiers.
    /// </summary>
    private ModelConfig? AttemptFallback(ModelTier startTier, string? preferredProvider)
    {
        var currentTier = startTier;
        var attempts = 0;

        while (attempts < _options.MaxFallbackAttempts)
        {
            var nextTier = (ModelTier)((int)currentTier + 1);

            if (!Enum.IsDefined(nextTier))
            {
                break;
            }

            var model = FindModelForTier(nextTier, preferredProvider);
            if (model is not null)
            {
                return model;
            }

            currentTier = nextTier;
            attempts++;
        }

        return null;
    }

    /// <summary>
    /// Returns a default Standard tier model as graceful degradation when all selection paths fail.
    /// </summary>
    private ModelConfig GetDefaultFallbackModel()
    {
        // Try to find any Standard tier model first
        var standard = _models.Values.FirstOrDefault(m => m.Tier == ModelTier.Standard);
        if (standard is not null)
        {
            return standard;
        }

        // Try any registered model
        var any = _models.Values.FirstOrDefault();
        if (any is not null)
        {
            return any;
        }

        // Absolute fallback — return a synthetic config
        return new ModelConfig("gpt-4o", "OpenAI", ModelTier.Standard, 0.0025m, 0.010m, 128000);
    }

    /// <summary>
    /// Registers default model configurations for each tier.
    /// </summary>
    private void RegisterDefaultModels()
    {
        RegisterModel(new ModelConfig("gpt-4o-mini", "OpenAI", ModelTier.Fast, 0.00015m, 0.0006m, 128000));
        RegisterModel(new ModelConfig("gpt-4o", "OpenAI", ModelTier.Standard, 0.0025m, 0.010m, 128000));
        RegisterModel(new ModelConfig("claude-sonnet-4-20250514", "Anthropic", ModelTier.Premium, 0.003m, 0.015m, 200000));
        RegisterModel(new ModelConfig("claude-opus-4-20250514", "Anthropic", ModelTier.Expert, 0.015m, 0.075m, 200000));
    }

    /// <summary>
    /// Determines if a task type is considered simple for tier downgrade purposes.
    /// </summary>
    private static bool IsSimpleTaskType(string? taskType)
    {
        if (string.IsNullOrWhiteSpace(taskType))
        {
            return false;
        }

        return taskType.Equals("summarization", StringComparison.OrdinalIgnoreCase)
               || taskType.Equals("classification", StringComparison.OrdinalIgnoreCase)
               || taskType.Equals("extraction", StringComparison.OrdinalIgnoreCase)
               || taskType.Equals("simple", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the higher of two model tiers.
    /// </summary>
    private static ModelTier MaxTier(ModelTier a, ModelTier b)
    {
        return (ModelTier)Math.Max((int)a, (int)b);
    }
}
