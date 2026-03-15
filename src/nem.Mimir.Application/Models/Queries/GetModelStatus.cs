using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Models.Queries;

/// <summary>
/// Query to retrieve the status and metadata of a specific LLM model.
/// </summary>
/// <param name="ModelId">The identifier of the model to retrieve status for.</param>
public sealed record GetModelStatusQuery(string ModelId) : IQuery<LlmModelInfoDto?>;

internal sealed class GetModelStatusQueryHandler : IRequestHandler<GetModelStatusQuery, LlmModelInfoDto?>
{
    private const string ModelsListCacheKey = "available-models";
    private const int CacheExpirationSeconds = 60;

    private readonly ILlmService _llmService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GetModelStatusQueryHandler> _logger;

    public GetModelStatusQueryHandler(
        ILlmService llmService,
        IMemoryCache cache,
        ILogger<GetModelStatusQueryHandler> logger)
    {
        _llmService = llmService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<LlmModelInfoDto?> Handle(GetModelStatusQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving status for model: {ModelId}", request.ModelId);

        var models = await _cache.GetOrCreateAsync(
            ModelsListCacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirationSeconds);
                _logger.LogDebug("Cache miss for models list, fetching from LLM service");
                return await _llmService.GetAvailableModelsAsync(cancellationToken);
            });

        var model = models?.FirstOrDefault(m => string.Equals(m.Id, request.ModelId, StringComparison.OrdinalIgnoreCase));

        if (model is null)
        {
            _logger.LogDebug("Model not found: {ModelId}", request.ModelId);
        }

        return model;
    }
}
