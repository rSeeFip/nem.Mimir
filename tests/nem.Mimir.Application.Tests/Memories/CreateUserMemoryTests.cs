using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Memories.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Memories;

public sealed class CreateUserMemoryTests
{
    private readonly IUserMemoryRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly CreateUserMemoryCommandHandler _handler;

    public CreateUserMemoryTests()
    {
        _repository = Substitute.For<IUserMemoryRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _mapper = new MimirMapper();

        _handler = new CreateUserMemoryCommandHandler(_repository, _currentUserService, _unitOfWork, _mapper);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateAndReturnDto()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _repository.CreateAsync(Arg.Any<UserMemory>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<UserMemory>());

        var result = await _handler.Handle(
            new CreateUserMemoryCommand("Remember I prefer markdown.", "response preferences"),
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.UserId.ShouldBe(userId);
        result.Content.ShouldBe("Remember I prefer markdown.");
        result.Context.ShouldBe("response preferences");
        result.Source.ShouldBe("manual");
        result.IsActive.ShouldBeTrue();

        await _repository.Received(1).CreateAsync(Arg.Any<UserMemory>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyContent_ShouldFail()
    {
        var validator = new CreateUserMemoryCommandValidator();
        var command = new CreateUserMemoryCommand("", "ctx");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Content");
    }

    [Fact]
    public void Validator_ContentTooLong_ShouldFail()
    {
        var validator = new CreateUserMemoryCommandValidator();
        var command = new CreateUserMemoryCommand(new string('x', 4001), "ctx");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Content");
    }
}
