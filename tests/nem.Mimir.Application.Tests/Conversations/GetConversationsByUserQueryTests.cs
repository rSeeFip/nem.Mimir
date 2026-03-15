using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Queries;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class GetConversationsByUserQueryTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;
    private readonly GetConversationsByUserQueryHandler _handler;

    public GetConversationsByUserQueryTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        _mapper = new MimirMapper();

        _handler = new GetConversationsByUserQueryHandler(_repository, _currentUserService, _mapper);
    }

    [Fact]
    public async Task Handle_ValidQuery_ShouldReturnPaginatedConversations()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var conversations = new List<Conversation>
        {
            Conversation.Create(userId, "Conversation 1"),
            Conversation.Create(userId, "Conversation 2"),
        };

        var paginatedResult = new PaginatedList<Conversation>(
            conversations.AsReadOnly(), 1, 1, 2);

        _repository.GetByUserIdAsync(userId, 1, 10, Arg.Any<CancellationToken>())
            .Returns(paginatedResult);

        var query = new GetConversationsByUserQuery(1, 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.PageNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        var query = new GetConversationsByUserQuery(1, 10);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public void Validator_PageNumberZero_ShouldFail()
    {
        // Arrange
        var validator = new GetConversationsByUserQueryValidator();
        var query = new GetConversationsByUserQuery(0, 10);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PageNumber");
    }

    [Fact]
    public void Validator_PageSizeTooLarge_ShouldFail()
    {
        // Arrange
        var validator = new GetConversationsByUserQueryValidator();
        var query = new GetConversationsByUserQuery(1, 51);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Validator_PageSizeZero_ShouldFail()
    {
        // Arrange
        var validator = new GetConversationsByUserQueryValidator();
        var query = new GetConversationsByUserQuery(1, 0);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Validator_ValidQuery_ShouldPass()
    {
        // Arrange
        var validator = new GetConversationsByUserQueryValidator();
        var query = new GetConversationsByUserQuery(1, 25);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
