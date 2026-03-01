using Mimir.Domain.ValueObjects;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.SystemPrompts.Queries;
using Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.SystemPrompts;

public sealed class GetSystemPromptByIdQueryTests
{
    private readonly ISystemPromptRepository _repository;
    private readonly MimirMapper _mapper;
    private readonly GetSystemPromptByIdQueryHandler _handler;

    public GetSystemPromptByIdQueryTests()
    {
        _repository = Substitute.For<ISystemPromptRepository>();

        _mapper = new MimirMapper();

        _handler = new GetSystemPromptByIdQueryHandler(_repository, _mapper);
    }

    [Fact]
    public async Task Handle_ExistingPrompt_ShouldReturnDto()
    {
        // Arrange
        var prompt = SystemPrompt.Create("Test", "Template {{var}}", "Description");
        var id = prompt.Id;

        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(prompt);

        var query = new GetSystemPromptByIdQuery(id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(id.Value);
        result.Name.ShouldBe("Test");
        result.Template.ShouldBe("Template {{var}}");
        result.Description.ShouldBe("Description");
        result.IsActive.ShouldBeTrue();
        result.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentPrompt_ShouldThrowNotFoundException()
    {
        // Arrange
        var id = SystemPromptId.New();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((SystemPrompt?)null);

        var query = new GetSystemPromptByIdQuery(id);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }
}
