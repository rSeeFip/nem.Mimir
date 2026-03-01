using Mimir.Domain.ValueObjects;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.SystemPrompts.Queries;
using Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.SystemPrompts;

public sealed class RenderSystemPromptQueryTests
{
    private readonly ISystemPromptRepository _repository;
    private readonly ISystemPromptService _systemPromptService;
    private readonly RenderSystemPromptQueryHandler _handler;

    public RenderSystemPromptQueryTests()
    {
        _repository = Substitute.For<ISystemPromptRepository>();
        _systemPromptService = Substitute.For<ISystemPromptService>();

        _handler = new RenderSystemPromptQueryHandler(_repository, _systemPromptService);
    }

    [Fact]
    public async Task Handle_ExistingPrompt_ShouldRenderTemplate()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Test", "Hello {{name}}, welcome to {{app}}", "Desc");
        var id = prompt.Id;
        var variables = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["app"] = "Mimir"
        };

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(prompt);

        _systemPromptService.RenderTemplate(prompt.Template, Arg.Any<IDictionary<string, string>>())
            .Returns("Hello Alice, welcome to Mimir");

        var query = new RenderSystemPromptQuery(id, variables);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBe("Hello Alice, welcome to Mimir");
        _systemPromptService.Received(1).RenderTemplate(prompt.Template, Arg.Any<IDictionary<string, string>>());
    }

    [Fact]
    public async Task Handle_NonExistentPrompt_ShouldThrowNotFoundException()
    {
        // Arrange
        var id = SystemPromptId.New();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((SystemPrompt?)null);

        var query = new RenderSystemPromptQuery(id, new Dictionary<string, string>());

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmptyVariables_ShouldStillRender()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Test", "No variables here", "Desc");
        var id = prompt.Id;
        var variables = new Dictionary<string, string>();

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(prompt);

        _systemPromptService.RenderTemplate(prompt.Template, Arg.Any<IDictionary<string, string>>())
            .Returns("No variables here");

        var query = new RenderSystemPromptQuery(id, variables);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBe("No variables here");
    }
}
