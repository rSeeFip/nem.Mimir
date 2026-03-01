using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Application.OpenAiCompat.Queries;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.OpenAiCompat;

public sealed class ListOpenAiModelsQueryTests
{
    private readonly ILlmService _llmService;
    private readonly ListOpenAiModelsQueryHandler _handler;

    public ListOpenAiModelsQueryTests()
    {
        _llmService = Substitute.For<ILlmService>();
        _handler = new ListOpenAiModelsQueryHandler(_llmService);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyAvailableModels()
    {
        // Arrange
        var models = new List<LlmModelInfoDto>
        {
            new("phi-4-mini", "Phi 4 Mini", 16384, "Fast responses", true),
            new("unavailable-model", "Unavailable", 4096, "Offline", false),
            new("qwen-2.5-72b", "Qwen 2.5 72B", 131072, "Complex reasoning", true),
        };
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(models);

        var query = new ListOpenAiModelsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(m => m.Id == "phi-4-mini" || m.Id == "qwen-2.5-72b");
    }

    [Fact]
    public async Task Handle_NoAvailableModels_ReturnsEmptyList()
    {
        // Arrange
        var models = new List<LlmModelInfoDto>
        {
            new("model-a", "Model A", 4096, "Offline", false),
        };
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(models);

        var query = new ListOpenAiModelsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_EmptyModelList_ReturnsEmptyList()
    {
        // Arrange
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<LlmModelInfoDto>());

        var query = new ListOpenAiModelsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_SetsOwnedByToMimir()
    {
        // Arrange
        var models = new List<LlmModelInfoDto>
        {
            new("phi-4-mini", "Phi 4 Mini", 16384, "Fast responses", true),
        };
        _llmService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(models);

        var query = new ListOpenAiModelsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe("phi-4-mini");
        result[0].OwnedBy.ShouldBe("mimir");
    }
}
