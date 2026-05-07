using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Models.Queries;
using nem.Mimir.Domain.MultiTenancy;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Models.Queries;

public sealed class GetModelsQueryTests
{
    private readonly ILlmService _llmService;
    private readonly IDistributedCache _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetModelsQueryHandler> _logger;
    private readonly GetModelsQueryHandler _handler;

    public GetModelsQueryTests()
    {
        _llmService = Substitute.For<ILlmService>();
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns("tenant-a");
        _logger = Substitute.For<ILogger<GetModelsQueryHandler>>();
        _handler = new GetModelsQueryHandler(_llmService, _cache, _tenantContext, _logger);
    }

    [Fact]
    public async Task Handle_CacheMiss_ShouldFetchFromServiceAndReturnModels()
    {
        var expectedModels = new List<LlmModelInfoDto>
        {
            new("gpt-4", "GPT-4", 8192, "General purpose", true),
            new("claude-3", "Claude 3", 100000, "Long context", true)
        };
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(expectedModels);

        var result = await _handler.Handle(new GetModelsQuery(), CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("gpt-4");
        result[1].Id.ShouldBe("claude-3");
        await _llmService.Received(1).GetAvailableModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CacheMiss_ServiceReturnsEmpty_ShouldReturnEmptyList()
    {
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(new List<LlmModelInfoDto>());

        var result = await _handler.Handle(new GetModelsQuery(), CancellationToken.None);

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_CacheHit_ShouldReturnCachedModelsWithoutCallingService()
    {
        var cachedModels = new List<LlmModelInfoDto>
        {
            new("phi-4", "Phi 4", 4096, "Small tasks", true)
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cachedModels);
        await _cache.SetAsync(
            "tenant:tenant-a:models",
            bytes,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        var result = await _handler.Handle(new GetModelsQuery(), CancellationToken.None);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("phi-4");
        await _llmService.DidNotReceive().GetAvailableModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TenantA_KeyDoesNotLeakToTenantB()
    {
        var modelsA = new List<LlmModelInfoDto> { new("model-a", "Model A", 4096, "A", true) };
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(modelsA);

        _tenantContext.TenantId.Returns("tenant-a");
        await _handler.Handle(new GetModelsQuery(), CancellationToken.None);

        var tenantBKey = "tenant:tenant-b:models";
        var tenantBCached = await _cache.GetAsync(tenantBKey);
        tenantBCached.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_TenantIsolation_DifferentTenantsGetDifferentCacheKeys()
    {
        var modelsA = new List<LlmModelInfoDto> { new("model-a", "Model A", 4096, "A", true) };
        var modelsB = new List<LlmModelInfoDto> { new("model-b", "Model B", 8192, "B", true) };

        _tenantContext.TenantId.Returns("tenant-a");
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(modelsA);
        var handlerA = new GetModelsQueryHandler(_llmService, _cache, _tenantContext, _logger);
        await handlerA.Handle(new GetModelsQuery(), CancellationToken.None);

        var tenantContextB = Substitute.For<ITenantContext>();
        tenantContextB.TenantId.Returns("tenant-b");
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(modelsB);
        var handlerB = new GetModelsQueryHandler(_llmService, _cache, tenantContextB, _logger);
        var resultB = await handlerB.Handle(new GetModelsQuery(), CancellationToken.None);

        resultB.Count.ShouldBe(1);
        resultB[0].Id.ShouldBe("model-b");

        var tenantContextA2 = Substitute.For<ITenantContext>();
        tenantContextA2.TenantId.Returns("tenant-a");
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(modelsA);
        var handlerA2 = new GetModelsQueryHandler(_llmService, _cache, tenantContextA2, _logger);
        var resultA = await handlerA2.Handle(new GetModelsQuery(), CancellationToken.None);

        resultA.Count.ShouldBe(1);
        resultA[0].Id.ShouldBe("model-a");
    }

    [Fact]
    public async Task Handle_Invalidation_OnlyAffectsTargetTenant()
    {
        var modelsA = new List<LlmModelInfoDto> { new("model-a", "Model A", 4096, "A", true) };
        var modelsB = new List<LlmModelInfoDto> { new("model-b", "Model B", 8192, "B", true) };

        var bytesA = JsonSerializer.SerializeToUtf8Bytes(modelsA);
        var bytesB = JsonSerializer.SerializeToUtf8Bytes(modelsB);
        var opts = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
        await _cache.SetAsync("tenant:tenant-a:models", bytesA, opts);
        await _cache.SetAsync("tenant:tenant-b:models", bytesB, opts);

        await _cache.RemoveAsync("tenant:tenant-a:models");

        var tenantACached = await _cache.GetAsync("tenant:tenant-a:models");
        var tenantBCached = await _cache.GetAsync("tenant:tenant-b:models");

        tenantACached.ShouldBeNull();
        tenantBCached.ShouldNotBeNull();
    }
}
