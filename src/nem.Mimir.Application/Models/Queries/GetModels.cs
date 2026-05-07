using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.MultiTenancy;

namespace nem.Mimir.Application.Models.Queries;

public sealed record GetModelsQuery : IQuery<IReadOnlyList<LlmModelInfoDto>>;

internal sealed class GetModelsQueryHandler : IRequestHandler<GetModelsQuery, IReadOnlyList<LlmModelInfoDto>>
{
    private static readonly TimeSpan ModelsTtl = TimeSpan.FromMinutes(5);

    private readonly ILlmService _llmService;
    private readonly IDistributedCache _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetModelsQueryHandler> _logger;

    public GetModelsQueryHandler(
        ILlmService llmService,
        IDistributedCache cache,
        ITenantContext tenantContext,
        ILogger<GetModelsQueryHandler> logger)
    {
        _llmService = llmService;
        _cache = cache;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LlmModelInfoDto>> Handle(GetModelsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? "default";
        var cacheKey = $"tenant:{tenantId}:models";

        _logger.LogDebug("Retrieving available models for tenant {TenantId} (cached)", tenantId);

        var cached = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for models list (tenant {TenantId})", tenantId);
            return JsonSerializer.Deserialize<List<LlmModelInfoDto>>(cached) ?? [];
        }

        _logger.LogDebug("Cache miss for models list, fetching from LLM service (tenant {TenantId})", tenantId);
        var models = await _llmService.GetAvailableModelsAsync(cancellationToken);

        if (models is not null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(models);
            var entryOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ModelsTtl };
            await _cache.SetAsync(cacheKey, bytes, entryOptions, cancellationToken);
        }

        return models ?? [];
    }
}
