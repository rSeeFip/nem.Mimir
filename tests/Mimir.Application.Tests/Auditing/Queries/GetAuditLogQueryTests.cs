using AutoMapper;
using Mimir.Application.Auditing.Queries;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Auditing.Queries;

public sealed class GetAuditLogQueryTests
{
    private readonly IAuditRepository _auditRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly GetAuditLogQueryHandler _handler;

    public GetAuditLogQueryTests()
    {
        _auditRepository = Substitute.For<IAuditRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = mapperConfig.CreateMapper();

        _handler = new GetAuditLogQueryHandler(_auditRepository, _currentUserService, _mapper);
    }

    [Fact]
    public async Task Handle_AdminWithNoFilter_ShouldReturnAllAuditEntries()
    {
        // Arrange
        var entries = new List<AuditEntry>
        {
            AuditEntry.Create(Guid.NewGuid(), "UserCreated", "User", Guid.NewGuid().ToString()),
            AuditEntry.Create(Guid.NewGuid(), "ConversationCreated", "Conversation", Guid.NewGuid().ToString())
        };

        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "Admin" });
        _auditRepository.GetAllAsync(1, 20, Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<AuditEntry>(entries.AsReadOnly(), 1, 1, 2));

        var query = new GetAuditLogQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.PageNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_AdminWithUserIdFilter_ShouldReturnFilteredEntries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entries = new List<AuditEntry>
        {
            AuditEntry.Create(userId, "UserCreated", "User", userId.ToString())
        };

        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "Admin" });
        _auditRepository.GetByUserIdAsync(userId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<AuditEntry>(entries.AsReadOnly(), 1, 1, 1));

        var query = new GetAuditLogQuery(UserId: userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(1);
        await _auditRepository.Received(1).GetByUserIdAsync(userId, 1, 20, Arg.Any<CancellationToken>());
        await _auditRepository.DidNotReceive().GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonAdmin_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "User" });

        var query = new GetAuditLogQuery();

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

        var query = new GetAuditLogQuery();

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AdminWithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var entries = new List<AuditEntry>
        {
            AuditEntry.Create(Guid.NewGuid(), "MessageSent", "Message", Guid.NewGuid().ToString())
        };

        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "Admin" });
        _auditRepository.GetAllAsync(2, 10, Arg.Any<CancellationToken>())
            .Returns(new PaginatedList<AuditEntry>(entries.AsReadOnly(), 2, 5, 50));

        var query = new GetAuditLogQuery(PageNumber: 2, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(50);
        result.PageNumber.ShouldBe(2);
        result.TotalPages.ShouldBe(5);
    }

    [Fact]
    public void Validator_PageNumberZero_ShouldFail()
    {
        // Arrange
        var validator = new GetAuditLogQueryValidator();
        var query = new GetAuditLogQuery(PageNumber: 0);

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
        var validator = new GetAuditLogQueryValidator();
        var query = new GetAuditLogQuery(PageSize: 101);

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
        var validator = new GetAuditLogQueryValidator();
        var query = new GetAuditLogQuery(PageSize: 0);

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
        var validator = new GetAuditLogQueryValidator();
        var query = new GetAuditLogQuery();

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_MaxPageSize_ShouldPass()
    {
        // Arrange
        var validator = new GetAuditLogQueryValidator();
        var query = new GetAuditLogQuery(PageSize: 100);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
