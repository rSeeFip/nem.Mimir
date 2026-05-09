namespace nem.Mimir.Infrastructure.Guardrails;

using System.Runtime.CompilerServices;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;

internal sealed class GuardrailLlmServiceDecorator(
    ILlmService inner,
    IGuardrailEvaluator evaluator,
    IGuardrailBundle? bundle = null) : ILlmService
{
    private const string ServiceId = "nem.mimir";
    private const string GuardrailDeniedFinishReason = "guardrail_denied";

    public async Task<LlmResponse> SendMessageAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var denial = await EvaluateAsync(model, messages, cancellationToken).ConfigureAwait(false);
        if (denial is not null)
        {
            return CreateDeniedResponse(model, denial);
        }

        return await inner.SendMessageAsync(model, messages, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamMessageAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var denial = await EvaluateAsync(model, messages, cancellationToken).ConfigureAwait(false);
        if (denial is not null)
        {
            yield return new LlmStreamChunk(denial, ResolveModelId(model), GuardrailDeniedFinishReason);
            yield break;
        }

        await foreach (var chunk in inner.StreamMessageAsync(model, messages, cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    public Task<IReadOnlyList<LlmModelInfoDto>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        => inner.GetAvailableModelsAsync(cancellationToken);

    private async Task<string?> EvaluateAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        CancellationToken cancellationToken)
    {
        if (bundle is null)
        {
            return null;
        }

        var result = await evaluator.EvaluateAsync(
            CreateRequest(model, messages),
            bundle,
            cancellationToken).ConfigureAwait(false);

        return result.Allowed
            ? null
            : result.Reason ?? "Guardrail policy blocked the request.";
    }

    private static GuardrailRequest CreateRequest(string model, IReadOnlyList<LlmMessage> messages)
        => new(
            ServiceId,
            ResolveModelId(model),
            BuildPrompt(messages),
            null,
            DateTimeOffset.UtcNow);

    private static string BuildPrompt(IReadOnlyList<LlmMessage> messages)
        => string.Join(Environment.NewLine, messages.Select(static message => $"{message.Role}: {message.Content}"));

    private static LlmResponse CreateDeniedResponse(string model, string reason)
        => new(
            reason,
            ResolveModelId(model),
            0,
            0,
            0,
            GuardrailDeniedFinishReason);

    private static string ResolveModelId(string? model)
        => string.IsNullOrWhiteSpace(model) ? string.Empty : model;
}
