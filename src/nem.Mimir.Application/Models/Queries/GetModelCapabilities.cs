using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Models.Queries;

public sealed record GetModelCapabilitiesQuery(string ModelId) : IQuery<ModelCapabilityDto>;

internal sealed class GetModelCapabilitiesQueryHandler
{
    private readonly ILlmService _llmService;

    public GetModelCapabilitiesQueryHandler(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task<ModelCapabilityDto> Handle(GetModelCapabilitiesQuery request, CancellationToken cancellationToken)
    {
        var normalizedModelId = request.ModelId.Trim();
        var models = await _llmService.GetAvailableModelsAsync(cancellationToken).ConfigureAwait(false);
        var model = models.FirstOrDefault(m => string.Equals(m.Id, normalizedModelId, StringComparison.OrdinalIgnoreCase));

        if (model is null)
        {
            throw new NotFoundException("Model", normalizedModelId);
        }

        var capability = InferCapability(model);

        return new ModelCapabilityDto(
            normalizedModelId,
            capability.SupportsVision,
            capability.SupportsFunctionCalling,
            capability.SupportsStreaming,
            capability.SupportsJsonMode);
    }

    private static ModelCapability InferCapability(LlmModelInfoDto model)
    {
        var id = model.Id.ToLowerInvariant();

        var supportsVision = id.Contains("vision", StringComparison.Ordinal) || id.Contains("gpt-4o", StringComparison.Ordinal);
        var supportsFunctionCalling = !id.Contains("embed", StringComparison.Ordinal);
        var supportsStreaming = true;
        var supportsJsonMode = id.Contains("gpt", StringComparison.Ordinal)
            || id.Contains("claude", StringComparison.Ordinal)
            || id.Contains("qwen", StringComparison.Ordinal);

        return new ModelCapability(
            supportsVision,
            supportsFunctionCalling,
            supportsStreaming,
            supportsJsonMode);
    }
}
