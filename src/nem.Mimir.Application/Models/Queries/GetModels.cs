using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Models.Queries;

/// <summary>
/// Query to retrieve the list of available LLM models from the service, with caching.
/// </summary>
public sealed record GetModelsQuery : IQuery<IReadOnlyList<LlmModelInfoDto>>;

internal sealed class GetModelsQueryHandler : IRequestHandler<GetModelsQuery, IReadOnlyList<LlmModelInfoDto>>
{
    private const string ModelsListCacheKey = "available-models";
    private const int CacheExpirationSeconds = 60;

    private readonly ILlmService _llmService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GetModelsQueryHandler> _logger;

    public GetModelsQueryHandler(
        ILlmService llmService,
        IMemoryCache cache,
        ILogger<GetModelsQueryHandler> logger)
    {
        _llmService = llmService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LlmModelInfoDto>> Handle(GetModelsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving available models (cached)");

        var models = await _cache.GetOrCreateAsync(
            ModelsListCacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirationSeconds);
                _logger.LogDebug("Cache miss for models list, fetching from LLM service");
                return await _llmService.GetAvailableModelsAsync(cancellationToken);
            });

        return models ?? Array.Empty<LlmModelInfoDto>();
    }
}
