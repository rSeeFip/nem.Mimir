using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Models.Queries;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Models.Queries;

public sealed class GetModelStatusQueryTests
{
    private readonly ILlmService _llmService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GetModelStatusQueryHandler> _logger;
    private readonly GetModelStatusQueryHandler _handler;

    public GetModelStatusQueryTests()
    {
        _llmService = Substitute.For<ILlmService>();
        _cache = Substitute.For<IMemoryCache>();
        _logger = Substitute.For<ILogger<GetModelStatusQueryHandler>>();
        _handler = new GetModelStatusQueryHandler(_llmService, _cache, _logger);
    }

    [Fact]
    public async Task Handle_ModelExists_ShouldReturnModel()
    {
        // Arrange
        var models = new List<LlmModelInfoDto>
        {
            new("gpt-4", "GPT-4", 8192, "General purpose", true),
            new("claude-3", "Claude 3", 100000, "Long context", true)
        };

        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(models);

        SetupCacheMiss();

        var query = new GetModelStatusQuery("gpt-4");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("gpt-4");
        result.Name.ShouldBe("GPT-4");
        result.ContextWindow.ShouldBe(8192);
    }

    [Fact]
    public async Task Handle_ModelNotFound_ShouldReturnNull()
    {
        // Arrange
        var models = new List<LlmModelInfoDto>
        {
            new("gpt-4", "GPT-4", 8192, "General purpose", true)
        };

        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(models);

        SetupCacheMiss();

        var query = new GetModelStatusQuery("non-existent-model");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_CaseInsensitiveMatch_ShouldReturnModel()
    {
        // Arrange
        var models = new List<LlmModelInfoDto>
        {
            new("gpt-4", "GPT-4", 8192, "General purpose", true)
        };

        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(models);

        SetupCacheMiss();

        var query = new GetModelStatusQuery("GPT-4");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("gpt-4");
    }

    [Fact]
    public async Task Handle_EmptyModelList_ShouldReturnNull()
    {
        // Arrange
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<LlmModelInfoDto>());

        SetupCacheMiss();

        var query = new GetModelStatusQuery("gpt-4");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_CacheHit_ShouldReturnCachedModelWithoutCallingService()
    {
        // Arrange
        var cachedModels = new List<LlmModelInfoDto>
        {
            new("phi-4", "Phi 4", 4096, "Small tasks", true)
        };

        SetupCacheHit(cachedModels);

        var query = new GetModelStatusQuery("phi-4");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("phi-4");
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
        _cache.TryGetValue(Arg.Any<object>(), out Arg.Any<object?>()!)
            .Returns(x =>
            {
                x[1] = cachedValue;
                return true;
            });
    }
}
