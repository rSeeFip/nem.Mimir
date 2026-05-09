using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using nem.Contracts.Identity;
using nem.Contracts.Inference;
using nem.Contracts.TokenOptimization;

namespace nem.Mimir.Infrastructure.Inference;

internal sealed class InferenceGatewayService(
    IModelCascade modelCascade,
    ILogger<InferenceGatewayService> logger) : IInferenceGateway
{
    private static readonly FrozenDictionary<string, ModelTier> DefaultAliasTierMap =
        new Dictionary<string, ModelTier>(StringComparer.OrdinalIgnoreCase)
        {
            ["fast"] = ModelTier.Fast,
            ["standard"] = ModelTier.Standard,
            ["premium"] = ModelTier.Premium,
            ["expert"] = ModelTier.Expert,
            ["coding"] = ModelTier.Premium,
            ["embedding"] = ModelTier.Fast,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, InferenceProviderId> ProviderIdMap =
        new Dictionary<string, InferenceProviderId>(StringComparer.OrdinalIgnoreCase)
        {
            ["OpenAI"] = InferenceProviderId.From(CreateDeterministicGuid("OpenAI")),
            ["Anthropic"] = InferenceProviderId.From(CreateDeterministicGuid("Anthropic")),
            ["LiteLLM"] = InferenceProviderId.From(CreateDeterministicGuid("LiteLLM")),
            ["Ollama"] = InferenceProviderId.From(CreateDeterministicGuid("Ollama")),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public async Task<InferenceResponse> SendAsync(
        InferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveModelInternalAsync(request.Alias, request.PreferredTier, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Inference gateway: alias={Alias} resolved to provider={Provider} model={ModelId} tier={Tier} correlation={CorrelationId}",
            request.Alias,
            resolved.Provider,
            resolved.ModelId,
            resolved.Tier,
            request.CorrelationId);

        return new InferenceResponse(
            Content: string.Empty,
            ResolvedAlias: request.Alias,
            ResolvedProviderId: resolved.ProviderId,
            ResolvedModelId: resolved.ModelId,
            ResolvedTier: resolved.Tier,
            CorrelationId: request.CorrelationId,
            PromptTokens: 0,
            CompletionTokens: 0,
            TotalTokens: 0,
            FinishReason: null);
    }

    public async IAsyncEnumerable<InferenceStreamChunk> StreamAsync(
        InferenceRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveModelInternalAsync(request.Alias, request.PreferredTier, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Inference gateway stream: alias={Alias} resolved to model={ModelId} tier={Tier}",
            request.Alias,
            resolved.ModelId,
            resolved.Tier);

        yield return new InferenceStreamChunk(
            Content: string.Empty,
            FinishReason: "resolved",
            ResolvedModelId: resolved.ModelId);
    }

    public async Task<ResolvedModel?> ResolveModelAsync(
        InferenceModelAlias alias,
        CancellationToken cancellationToken = default)
    {
        if (alias.IsEmpty)
        {
            return null;
        }

        try
        {
            return await ResolveModelInternalAsync(alias, preferredTier: null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Could not resolve inference model alias '{Alias}'", alias);
            return null;
        }
    }

    private async Task<ResolvedModel> ResolveModelInternalAsync(
        InferenceModelAlias alias,
        ModelTier? preferredTier,
        CancellationToken cancellationToken)
    {
        var tier = preferredTier ?? ResolveAliasToTier(alias);

        var context = new ModelSelectionContext(
            TaskType: alias.Value,
            EstimatedInputTokens: 0,
            EstimatedOutputTokens: 0,
            RequiresReasoning: tier >= ModelTier.Premium);

        var modelConfig = await modelCascade.SelectModelAsync(context, cancellationToken)
            .ConfigureAwait(false);

        var providerId = ResolveProviderId(modelConfig.Provider);

        return new ResolvedModel(
            Alias: alias,
            ProviderId: providerId,
            ModelId: modelConfig.ModelId,
            Provider: modelConfig.Provider,
            Tier: modelConfig.Tier,
            MaxContextWindow: modelConfig.MaxContextWindow);
    }

    internal static ModelTier ResolveAliasToTier(InferenceModelAlias alias)
    {
        if (alias.IsEmpty)
        {
            return ModelTier.Standard;
        }

        return DefaultAliasTierMap.TryGetValue(alias.Value, out var tier)
            ? tier
            : ModelTier.Standard;
    }

    private static InferenceProviderId ResolveProviderId(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return InferenceProviderId.Empty;
        }

        return ProviderIdMap.TryGetValue(providerName, out var id)
            ? id
            : InferenceProviderId.From(CreateDeterministicGuid(providerName));
    }

    private static Guid CreateDeterministicGuid(string input)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, 16));
    }
}
