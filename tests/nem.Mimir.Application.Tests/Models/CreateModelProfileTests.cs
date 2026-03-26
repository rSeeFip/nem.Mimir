using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Models.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Models;

public sealed class CreateModelProfileTests
{
    private readonly IModelProfileRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly CreateModelProfileCommandHandler _handler;

    public CreateModelProfileTests()
    {
        _repository = Substitute.For<IModelProfileRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _mapper = new MimirMapper();

        _handler = new CreateModelProfileCommandHandler(_repository, _currentUserService, _unitOfWork, _mapper);
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldCreateProfile()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        _repository.GetByNameForUserAsync(userId, "Creative", Arg.Any<CancellationToken>())
            .Returns((ModelProfile?)null);
        _repository.CreateAsync(Arg.Any<ModelProfile>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ModelProfile>());

        var result = await _handler.Handle(
            new CreateModelProfileCommand(
                "Creative",
                "gpt-4o",
                0.9m,
                0.95m,
                3000,
                0.1m,
                0.2m,
                ["END"],
                "Be creative.",
                "json"),
            CancellationToken.None);

        result.UserId.ShouldBe(userId);
        result.Name.ShouldBe("Creative");
        result.ModelId.ShouldBe("gpt-4o");
        result.MaxTokens.ShouldBe(3000);
        result.StopSequences.ShouldContain("END");

        await _repository.Received(1).CreateAsync(Arg.Any<ModelProfile>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateName_ShouldThrowConflict()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _repository.GetByNameForUserAsync(userId, "Creative", Arg.Any<CancellationToken>())
            .Returns(ModelProfile.Create(userId, "Creative", "gpt-4o", ModelParameters.Default));

        await Should.ThrowAsync<ConflictException>(() =>
            _handler.Handle(
                new CreateModelProfileCommand("Creative", "gpt-4o", null, null, null, null, null, null, null, null),
                CancellationToken.None));
    }

    [Fact]
    public void Validator_InvalidTemperature_ShouldFail()
    {
        var validator = new CreateModelProfileCommandValidator();

        var result = validator.Validate(
            new CreateModelProfileCommand("Profile", "model", 2.5m, null, null, null, null, null, null, null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Temperature");
    }
}
