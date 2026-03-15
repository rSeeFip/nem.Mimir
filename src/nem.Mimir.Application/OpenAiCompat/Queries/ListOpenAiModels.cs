using MediatR;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.OpenAiCompat.Models;

namespace nem.Mimir.Application.OpenAiCompat.Queries;

/// <summary>
/// Query to list available models in OpenAI-compatible format.
/// </summary>
public sealed record ListOpenAiModelsQuery : IQuery<List<OpenAiModelDto>>;

/// <summary>
/// Handles <see cref="ListOpenAiModelsQuery"/> by fetching available models
/// from the LLM service and mapping them to OpenAI-compatible DTOs.
/// </summary>
internal sealed class ListOpenAiModelsQueryHandler : IRequestHandler<ListOpenAiModelsQuery, List<OpenAiModelDto>>
{
    private readonly ILlmService _llmService;

    public ListOpenAiModelsQueryHandler(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task<List<OpenAiModelDto>> Handle(ListOpenAiModelsQuery request, CancellationToken cancellationToken)
    {
        var models = await _llmService.GetAvailableModelsAsync(cancellationToken);

        return models
            .Where(m => m.IsAvailable)
            .Select(m => new OpenAiModelDto(m.Id))
            .ToList();
    }
}
