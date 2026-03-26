using nem.Mimir.Application.Channels.Commands;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Channels;

public sealed class CreateChannelTests
{
    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateChannelAndReturnDto()
    {
        var repository = Substitute.For<IChannelRepository>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mapper = new MimirMapper();
        var handler = new CreateChannelHandler();

        var userId = Guid.NewGuid();
        currentUserService.UserId.Returns(userId.ToString());

        repository.CreateAsync(Arg.Any<Channel>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Channel>());

        var command = new CreateChannelCommand("general", "General discussion", "Public");

        var result = await handler.Handle(command, repository, currentUserService, unitOfWork, mapper, CancellationToken.None);

        result.Name.ShouldBe("general");
        result.CreatedByUserId.ShouldBe(userId);
        result.AccessControl.ShouldBe("Public");

        await repository.Received(1).CreateAsync(Arg.Any<Channel>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ShouldThrowForbiddenAccessException()
    {
        var repository = Substitute.For<IChannelRepository>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var mapper = new MimirMapper();
        var handler = new CreateChannelHandler();

        currentUserService.UserId.Returns((string?)null);

        var command = new CreateChannelCommand("general", null, "Public");

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => handler.Handle(command, repository, currentUserService, unitOfWork, mapper, CancellationToken.None));
    }

    [Fact]
    public void Validator_EmptyName_ShouldFail()
    {
        var validator = new CreateChannelCommandValidator();
        var command = new CreateChannelCommand(string.Empty, null, "Public");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == "Name");
    }
}
