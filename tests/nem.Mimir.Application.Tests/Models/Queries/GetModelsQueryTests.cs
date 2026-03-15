using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Models.Queries;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Models.Queries;

public sealed class GetModelsQueryTests
{
    private readonly ILlmService _llmService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GetModelsQueryHandler> _logger;
    private readonly GetModelsQueryHandler _handler;

    public GetModelsQueryTests()
    {
        _llmService = Substitute.For<ILlmService>();
        _cache = Substitute.For<IMemoryCache>();
        _logger = Substitute.For<ILogger<GetModelsQueryHandler>>();
        _handler = new GetModelsQueryHandler(_llmService, _cache, _logger);
    }

    [Fact]
    public async Task Handle_CacheMiss_ShouldFetchFromServiceAndReturnModels()
    {
        // Arrange
        var expectedModels = new List<LlmModelInfoDto>
        {
            new("gpt-4", "GPT-4", 8192, "General purpose", true),
            new("claude-3", "Claude 3", 100000, "Long context", true)
        };

        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(expectedModels);

        SetupCacheMiss();

        var query = new GetModelsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("gpt-4");
        result[1].Id.ShouldBe("claude-3");
        await _llmService.Received(1).GetAvailableModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CacheMiss_ServiceReturnsEmpty_ShouldReturnEmptyList()
    {
        // Arrange
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<LlmModelInfoDto>());

        SetupCacheMiss();

        var query = new GetModelsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_CacheHit_ShouldReturnCachedModelsWithoutCallingService()
    {
        // Arrange
        var cachedModels = new List<LlmModelInfoDto>
        {
            new("phi-4", "Phi 4", 4096, "Small tasks", true)
        };

        SetupCacheHit(cachedModels);

        var query = new GetModelsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("phi-4");
        await _llmService.DidNotReceive().GetAvailableModelsAsync(Arg.Any<CancellationToken>());
    }

    private void SetupCacheMiss()
    {
        object? outVal = null;
        _cache.TryGetValue(Arg.Any<object>(), out outVal).Returns(false);

        var cacheEntry = Substitute.For<ICacheEntry>();
        _cache.CreateEntry(Arg.Any<object>()).Returns(cacheEntry);
    }

    private void SetupCacheHit(IReadOnlyList<LlmModelInfoDto> cachedValue)
    {
        object outVal = cachedValue;
        _cache.TryGetValue(Arg.Any<object>(), out Arg.Any<object?>()!)
            .Returns(x =>
            {
                x[1] = cachedValue;
                return true;
            });
    }
}
