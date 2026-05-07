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

public sealed class GetModelStatusQueryTests
{
    private readonly ILlmService _llmService;
    private readonly IDistributedCache _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetModelStatusQueryHandler> _logger;
    private readonly GetModelStatusQueryHandler _handler;

    public GetModelStatusQueryTests()
    {
        _llmService = Substitute.For<ILlmService>();
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns("tenant-a");
        _logger = Substitute.For<ILogger<GetModelStatusQueryHandler>>();
        _handler = new GetModelStatusQueryHandler(_llmService, _cache, _tenantContext, _logger);
    }

    [Fact]
    public async Task Handle_ModelExists_ShouldReturnModel()
    {
        var models = new List<LlmModelInfoDto>
        {
            new("gpt-4", "GPT-4", 8192, "General purpose", true),
            new("claude-3", "Claude 3", 100000, "Long context", true)
        };
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(models);

        var result = await _handler.Handle(new GetModelStatusQuery("gpt-4"), CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("gpt-4");
        result.Name.ShouldBe("GPT-4");
        result.ContextWindow.ShouldBe(8192);
    }

    [Fact]
    public async Task Handle_ModelNotFound_ShouldReturnNull()
    {
        var models = new List<LlmModelInfoDto>
        {
            new("gpt-4", "GPT-4", 8192, "General purpose", true)
        };
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(models);

        var result = await _handler.Handle(new GetModelStatusQuery("non-existent-model"), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_CaseInsensitiveMatch_ShouldReturnModel()
    {
        var models = new List<LlmModelInfoDto>
        {
            new("gpt-4", "GPT-4", 8192, "General purpose", true)
        };
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(models);

        var result = await _handler.Handle(new GetModelStatusQuery("GPT-4"), CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("gpt-4");
    }

    [Fact]
    public async Task Handle_EmptyModelList_ShouldReturnNull()
    {
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(new List<LlmModelInfoDto>());

        var result = await _handler.Handle(new GetModelStatusQuery("gpt-4"), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_CacheHit_ShouldReturnCachedModelWithoutCallingService()
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

        var result = await _handler.Handle(new GetModelStatusQuery("phi-4"), CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("phi-4");
        await _llmService.DidNotReceive().GetAvailableModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TenantA_CacheKeyDoesNotLeakToTenantB()
    {
        var models = new List<LlmModelInfoDto> { new("gpt-4", "GPT-4", 8192, "General purpose", true) };
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>()).Returns(models);

        _tenantContext.TenantId.Returns("tenant-a");
        await _handler.Handle(new GetModelStatusQuery("gpt-4"), CancellationToken.None);

        var tenantBCached = await _cache.GetAsync("tenant:tenant-b:models");
        tenantBCached.ShouldBeNull();
    }
}
