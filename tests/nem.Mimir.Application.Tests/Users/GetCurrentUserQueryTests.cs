using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Users.Queries;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Users;

public sealed class GetCurrentUserQueryTests
{
    private readonly IUserRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;
    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryTests()
    {
        _repository = Substitute.For<IUserRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        _mapper = new MimirMapper();

        _handler = new GetCurrentUserQueryHandler(_repository, _currentUserService, _mapper);
    }

    [Fact]
    public async Task Handle_AuthenticatedUser_ShouldReturnDto()
    {
        // Arrange
        var user = User.Create("testuser", "test@example.com", UserRole.User);
        _currentUserService.UserId.Returns(user.Id.ToString());
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var query = new GetCurrentUserQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Username.ShouldBe("testuser");
        result.Email.ShouldBe("test@example.com");
        result.Role.ShouldBe("User");
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Unauthenticated_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);

        var query = new GetCurrentUserQuery();

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UserNotFoundInDb_ShouldThrowNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var query = new GetCurrentUserQuery();

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }
}
