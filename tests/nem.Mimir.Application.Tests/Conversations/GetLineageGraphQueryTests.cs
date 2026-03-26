using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Conversations.Queries;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class GetLineageGraphQueryTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly GetLineageGraphQueryHandler _handler;

    public GetLineageGraphQueryTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _handler = new GetLineageGraphQueryHandler(_repository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingConversation_ShouldReturnRootTree()
    {
        var userId = Guid.NewGuid();
        var root = Conversation.Create(userId, "Root");
        var child1 = root.Fork(userId, "Fork A");
        var child2 = root.Fork(userId, "Fork B");
        var grandchild = child1.Fork(userId, "Fork A.1");

        _currentUserService.UserId.Returns(userId.ToString());
        _repository.GetAllByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Conversation> { root, child1, child2, grandchild }.AsReadOnly());

        var result = await _handler.Handle(new GetLineageGraphQuery(grandchild.Id), CancellationToken.None);

        result.ConversationId.ShouldBe(root.Id);
        result.Title.ShouldBe("Root");
        result.ParentConversationId.ShouldBeNull();
        result.Children.Count.ShouldBe(2);

        var branchA = result.Children.Single(c => c.ConversationId == child1.Id);
        branchA.ForkReason.ShouldBe("Fork A");
        branchA.Children.Count.ShouldBe(1);
        branchA.Children[0].ConversationId.ShouldBe(grandchild.Id);
        branchA.Children[0].ForkReason.ShouldBe("Fork A.1");
    }

    [Fact]
    public async Task Handle_ConversationNotInUserSet_ShouldThrowNotFoundException()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _repository.GetAllByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Conversation>());

        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(new GetLineageGraphQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ShouldThrowForbiddenAccessException()
    {
        _currentUserService.UserId.Returns((string?)null);

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(new GetLineageGraphQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyConversationId_ShouldFail()
    {
        var validator = new GetLineageGraphQueryValidator();

        var result = validator.Validate(new GetLineageGraphQuery(Guid.Empty));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ConversationId");
    }

    [Fact]
    public void Validator_ValidConversationId_ShouldPass()
    {
        var validator = new GetLineageGraphQueryValidator();

        var result = validator.Validate(new GetLineageGraphQuery(Guid.NewGuid()));

        result.IsValid.ShouldBeTrue();
    }
}
