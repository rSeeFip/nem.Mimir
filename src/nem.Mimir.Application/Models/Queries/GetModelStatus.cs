using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.MultiTenancy;

namespace nem.Mimir.Application.Models.Queries;

public sealed record GetModelStatusQuery(string ModelId) : IQuery<LlmModelInfoDto?>;

internal sealed class GetModelStatusQueryHandler : IRequestHandler<GetModelStatusQuery, LlmModelInfoDto?>
{
    private static readonly TimeSpan ModelsTtl = TimeSpan.FromMinutes(5);

    private readonly ILlmService _llmService;
    private readonly IDistributedCache _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetModelStatusQueryHandler> _logger;

    public GetModelStatusQueryHandler(
        ILlmService llmService,
        IDistributedCache cache,
        ITenantContext tenantContext,
        ILogger<GetModelStatusQueryHandler> logger)
    {
        _llmService = llmService;
        _cache = cache;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<LlmModelInfoDto?> Handle(GetModelStatusQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? "default";
        var cacheKey = $"tenant:{tenantId}:models";

        _logger.LogDebug("Retrieving status for model: {ModelId} (tenant {TenantId})", request.ModelId, tenantId);

        IReadOnlyList<LlmModelInfoDto>? models = null;

        var cached = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            models = JsonSerializer.Deserialize<List<LlmModelInfoDto>>(cached);
        }
        else
        {
            _logger.LogDebug("Cache miss for models list, fetching from LLM service (tenant {TenantId})", tenantId);
            models = await _llmService.GetAvailableModelsAsync(cancellationToken);

            if (models is not null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(models);
                var entryOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ModelsTtl };
                await _cache.SetAsync(cacheKey, bytes, entryOptions, cancellationToken);
            }
        }

        var model = models?.FirstOrDefault(m => string.Equals(m.Id, request.ModelId, StringComparison.OrdinalIgnoreCase));

        if (model is null)
        {
            _logger.LogDebug("Model not found: {ModelId}", request.ModelId);
        }

        return model;
    }
}
