using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Application.Users.Queries;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Users;

public sealed class GetUserByIdQueryTests
{
    private readonly IUserRepository _repository;
    private readonly MimirMapper _mapper;
    private readonly GetUserByIdQueryHandler _handler;

    public GetUserByIdQueryTests()
    {
        _repository = Substitute.For<IUserRepository>();

        _mapper = new MimirMapper();

        _handler = new GetUserByIdQueryHandler(_repository, _mapper);
    }

    [Fact]
    public async Task Handle_UserExists_ShouldReturnDto()
    {
        // Arrange
        var user = User.Create("testuser", "test@example.com", UserRole.User);
        _repository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var query = new GetUserByIdQuery(user.Id);

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
    public async Task Handle_UserNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var query = new GetUserByIdQuery(userId);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }
}
