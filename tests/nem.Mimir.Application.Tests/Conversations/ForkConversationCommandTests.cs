using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Events;
using NSubstitute;
using Shouldly;
using Wolverine;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class ForkConversationCommandTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageBus _messageBus;
    private readonly MimirMapper _mapper;
    private readonly ForkConversationCommandHandler _handler;

    public ForkConversationCommandTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _messageBus = Substitute.For<IMessageBus>();
        _mapper = new MimirMapper();
        _handler = new ForkConversationCommandHandler(_repository, _currentUserService, _unitOfWork, _mapper, _messageBus);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldForkConversationAndReturnDto()
    {
        var userId = Guid.NewGuid();
        var source = Conversation.Create(userId, "Root");
        _currentUserService.UserId.Returns(userId.ToString());
        _repository.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _repository.CreateAsync(Arg.Any<Conversation>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Conversation>());

        var command = new ForkConversationCommand(source.Id, "Branch for experiments");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.ShouldNotBeNull();
        result.UserId.ShouldBe(userId);
        result.Title.ShouldBe("Root (fork)");

        await _repository.Received(1).CreateAsync(
            Arg.Is<Conversation>(c =>
                c.ParentConversationId == source.Id &&
                c.ForkReason == "Branch for experiments" &&
                c.DomainEvents.OfType<ConversationForkedEvent>().Any()),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(Arg.Any<ConversationForkedEvent>());
    }

    [Fact]
    public async Task Handle_SourceConversationNotFound_ShouldThrowNotFoundException()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var conversationId = Guid.NewGuid();
        _repository.GetByIdAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(new ForkConversationCommand(conversationId, "reason"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SourceConversationOwnedByAnotherUser_ShouldThrowForbiddenAccessException()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var source = Conversation.Create(ownerId, "Root");
        _currentUserService.UserId.Returns(otherUserId.ToString());
        _repository.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(new ForkConversationCommand(source.Id, "reason"), CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyConversationId_ShouldFail()
    {
        var validator = new ForkConversationCommandValidator();

        var result = validator.Validate(new ForkConversationCommand(Guid.Empty, "reason"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ConversationId");
    }

    [Fact]
    public void Validator_MissingForkReason_ShouldFail()
    {
        var validator = new ForkConversationCommandValidator();

        var result = validator.Validate(new ForkConversationCommand(Guid.NewGuid(), null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ForkReason");
    }

    [Fact]
    public void Validator_ValidCommand_ShouldPass()
    {
        var validator = new ForkConversationCommandValidator();

        var result = validator.Validate(new ForkConversationCommand(Guid.NewGuid(), "reason"));

        result.IsValid.ShouldBeTrue();
    }
}
