namespace nem.Mimir.Infrastructure.LiteLlm;

using System.Runtime.CompilerServices;
using nem.Contracts.Identity;
using nem.Contracts.Inference;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Llm;
using nem.Mimir.Infrastructure.Inference;

internal sealed class PolicyLlmServiceDecorator(
    ILlmService inner,
    PolicyIntentOverlayService policyIntentOverlayService) : ILlmService
{
    public async Task<LlmResponse> SendMessageAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var executionPlan = await policyIntentOverlayService
            .CreateExecutionPlanAsync(CreateInferenceRequest(model, messages, stream: false), cancellationToken)
            .ConfigureAwait(false);

        if (executionPlan.DenialReason is not null)
        {
            return CreateDeniedResponse(executionPlan.Request.Alias, executionPlan.DenialReason);
        }

        return await inner
            .SendMessageAsync(MapAliasToModel(executionPlan.Request.Alias), messages, cancellationToken)
            .ConfigureAwait(false);
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamMessageAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var executionPlan = await policyIntentOverlayService
            .CreateExecutionPlanAsync(CreateInferenceRequest(model, messages, stream: true), cancellationToken)
            .ConfigureAwait(false);

        if (executionPlan.DenialReason is not null)
        {
            yield return new LlmStreamChunk(
                Content: executionPlan.DenialReason,
                Model: MapAliasToModel(executionPlan.Request.Alias),
                FinishReason: "policy_denied");
            yield break;
        }

        await foreach (var chunk in inner.StreamMessageAsync(
                           MapAliasToModel(executionPlan.Request.Alias),
                           messages,
                           cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    public Task<IReadOnlyList<LlmModelInfoDto>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        => inner.GetAvailableModelsAsync(cancellationToken);

    private static InferenceRequest CreateInferenceRequest(
        string model,
        IReadOnlyList<LlmMessage> messages,
        bool stream)
    {
        return new InferenceRequest(
            Alias: MapModelToAlias(model),
            Messages: messages.Select(message => new InferenceMessage(message.Role, message.Content)).ToArray(),
            CorrelationId: CorrelationId.New(),
            Stream: stream,
            PreferredTier: InferenceGatewayService.ResolveAliasToTier(MapModelToAlias(model)));
    }

    private static InferenceModelAlias MapModelToAlias(string model)
    {
        return string.IsNullOrWhiteSpace(model)
            ? InferenceModelAlias.Standard
            : model switch
            {
                var value when value.Equals(LlmModels.Fast, StringComparison.OrdinalIgnoreCase) => InferenceModelAlias.Fast,
                var value when value.Equals(LlmModels.Primary, StringComparison.OrdinalIgnoreCase) => InferenceModelAlias.Standard,
                var value when value.Equals(LlmModels.Coding, StringComparison.OrdinalIgnoreCase) => InferenceModelAlias.Coding,
                var value when value.Equals(LlmModels.Embedding, StringComparison.OrdinalIgnoreCase) => InferenceModelAlias.Embedding,
                _ => InferenceModelAlias.From(model),
            };
    }

    private static string MapAliasToModel(InferenceModelAlias alias)
    {
        return alias.Value.ToLowerInvariant() switch
        {
            "fast" => LlmModels.Fast,
            "standard" => LlmModels.Primary,
            "premium" => LlmModels.Primary,
            "expert" => LlmModels.Primary,
            "coding" => LlmModels.Coding,
            "embedding" => LlmModels.Embedding,
            _ => alias.Value,
        };
    }

    private static LlmResponse CreateDeniedResponse(InferenceModelAlias alias, string denialReason)
    {
        return new LlmResponse(
            Content: denialReason,
            Model: MapAliasToModel(alias),
            PromptTokens: 0,
            CompletionTokens: 0,
            TotalTokens: 0,
            FinishReason: "policy_denied");
    }
}
