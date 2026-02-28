using AutoMapper;
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

public sealed class GetAllUsersQueryTests
{
    private readonly IUserRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly GetAllUsersQueryHandler _handler;

    public GetAllUsersQueryTests()
    {
        _repository = Substitute.For<IUserRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = mapperConfig.CreateMapper();

        _handler = new GetAllUsersQueryHandler(_repository, _currentUserService, _mapper);
    }

    [Fact]
    public async Task Handle_AdminUser_ShouldReturnPaginatedUsers()
    {
        // Arrange
        var users = new List<User>
        {
            User.Create("user1", "user1@example.com", UserRole.User),
            User.Create("user2", "user2@example.com", UserRole.Admin)
        };

        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "Admin" });
        _repository.GetAllAsync(1, 20, Arg.Any<CancellationToken>())
            .Returns((users.AsReadOnly() as IReadOnlyList<User>, 2));

        var query = new GetAllUsersQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.PageNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "User" });

        var query = new GetAllUsersQuery();

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Unauthenticated_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(false);
        _currentUserService.Roles.Returns(new List<string>());

        var query = new GetAllUsersQuery();

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AdminWithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var users = new List<User>
        {
            User.Create("user1", "user1@example.com", UserRole.User)
        };

        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "Admin" });
        _repository.GetAllAsync(2, 10, Arg.Any<CancellationToken>())
            .Returns((users.AsReadOnly() as IReadOnlyList<User>, 15));

        var query = new GetAllUsersQuery(PageNumber: 2, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(15);
        result.PageNumber.ShouldBe(2);
        result.TotalPages.ShouldBe(2);
    }

    [Fact]
    public void Validator_PageNumberZero_ShouldFail()
    {
        // Arrange
        var validator = new GetAllUsersQueryValidator();
        var query = new GetAllUsersQuery(PageNumber: 0);

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
        var validator = new GetAllUsersQueryValidator();
        var query = new GetAllUsersQuery(PageSize: 51);

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
        var validator = new GetAllUsersQueryValidator();
        var query = new GetAllUsersQuery(PageSize: 0);

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
        var validator = new GetAllUsersQueryValidator();
        var query = new GetAllUsersQuery();

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
