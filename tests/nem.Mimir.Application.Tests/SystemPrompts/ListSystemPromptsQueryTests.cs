using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.SystemPrompts.Queries;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.SystemPrompts;

public sealed class ListSystemPromptsQueryTests
{
    private readonly ISystemPromptRepository _repository;
    private readonly MimirMapper _mapper;
    private readonly ListSystemPromptsQueryHandler _handler;

    public ListSystemPromptsQueryTests()
    {
        _repository = Substitute.For<ISystemPromptRepository>();

        _mapper = new MimirMapper();

        _handler = new ListSystemPromptsQueryHandler(_repository, _mapper);
    }

    [Fact]
    public async Task Handle_ReturnsPagedResults()
    {
        // Arrange
        var prompts = new List<SystemPrompt>
        {
            SystemPrompt.Create("Prompt 1", "Template 1", "Desc 1"),
            SystemPrompt.Create("Prompt 2", "Template 2", "Desc 2"),
        };
        var paginatedList = new PaginatedList<SystemPrompt>(
            prompts.AsReadOnly(), pageNumber: 1, totalPages: 1, totalCount: 2);

        _repository.GetAllAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(paginatedList);

        var query = new ListSystemPromptsQuery(1, 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.PageNumber.ShouldBe(1);
        result.TotalCount.ShouldBe(2);
        result.TotalPages.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_EmptyResults_ReturnsEmptyList()
    {
        // Arrange
        var paginatedList = new PaginatedList<SystemPrompt>(
            Array.Empty<SystemPrompt>(), pageNumber: 1, totalPages: 0, totalCount: 0);

        _repository.GetAllAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(paginatedList);

        var query = new ListSystemPromptsQuery(1, 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Count.ShouldBe(0);
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public void Validator_PageNumberZero_ShouldFail()
    {
        var validator = new ListSystemPromptsQueryValidator();
        var query = new ListSystemPromptsQuery(0, 10);

        var result = validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PageNumber");
    }

    [Fact]
    public void Validator_PageSizeZero_ShouldFail()
    {
        var validator = new ListSystemPromptsQueryValidator();
        var query = new ListSystemPromptsQuery(1, 0);

        var result = validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Validator_PageSizeTooLarge_ShouldFail()
    {
        var validator = new ListSystemPromptsQueryValidator();
        var query = new ListSystemPromptsQuery(1, 51);

        var result = validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Validator_ValidQuery_ShouldPass()
    {
        var validator = new ListSystemPromptsQueryValidator();
        var query = new ListSystemPromptsQuery(1, 10);

        var result = validator.Validate(query);

        result.IsValid.ShouldBeTrue();
    }
}
