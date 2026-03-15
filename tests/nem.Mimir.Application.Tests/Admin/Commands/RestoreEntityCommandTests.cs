using nem.Mimir.Application.Admin.Commands;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

using ValidationException = nem.Mimir.Application.Common.Exceptions.ValidationException;

namespace nem.Mimir.Application.Tests.Admin.Commands;

public sealed class RestoreEntityCommandTests
{
    private readonly IEntityRestoreRepository _restoreRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RestoreEntityCommandHandler _handler;

    public RestoreEntityCommandTests()
    {
        _restoreRepository = Substitute.For<IEntityRestoreRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _handler = new RestoreEntityCommandHandler(_restoreRepository, _unitOfWork);
    }

    private static T CreateDeletedEntity<T>(T entity) where T : BaseAuditableEntity<Guid>
    {
        var baseType = typeof(BaseAuditableEntity<Guid>);
        baseType.GetProperty(nameof(BaseAuditableEntity<Guid>.IsDeleted))!
            .SetValue(entity, true);
        baseType.GetProperty(nameof(BaseAuditableEntity<Guid>.DeletedAt))!
            .SetValue(entity, DateTimeOffset.UtcNow);
        return entity;
    }

    [Fact]
    public async Task Handle_DeletedConversation_ShouldRestoreSuccessfully()
    {
        // Arrange
        var conversation = CreateDeletedEntity(Conversation.Create(Guid.NewGuid(), "Test Title"));
        conversation.IsDeleted.ShouldBeTrue();

        _restoreRepository.GetByIdIncludingDeletedAsync("conversation", conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var command = new RestoreEntityCommand("conversation", conversation.Id);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _restoreRepository.Received(1).Restore(conversation);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeletedUser_ShouldRestoreSuccessfully()
    {
        // Arrange
        var user = CreateDeletedEntity(User.Create("testuser", "test@example.com", Domain.Enums.UserRole.User));
        user.IsDeleted.ShouldBeTrue();

        _restoreRepository.GetByIdIncludingDeletedAsync("user", user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new RestoreEntityCommand("user", user.Id);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _restoreRepository.Received(1).Restore(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeletedSystemPrompt_ShouldRestoreSuccessfully()
    {
        // Arrange
        var prompt = CreateDeletedEntity(SystemPrompt.Create("Test Prompt", "Template", "Description"));
        prompt.IsDeleted.ShouldBeTrue();

        _restoreRepository.GetByIdIncludingDeletedAsync("systemprompt", prompt.Id, Arg.Any<CancellationToken>())
            .Returns(prompt);

        var command = new RestoreEntityCommand("systemprompt", prompt.Id);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _restoreRepository.Received(1).Restore(prompt);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EntityNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        _restoreRepository.GetByIdIncludingDeletedAsync("conversation", entityId, Arg.Any<CancellationToken>())
            .Returns((BaseAuditableEntity<Guid>?)null);

        var command = new RestoreEntityCommand("conversation", entityId);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EntityNotSoftDeleted_ShouldThrowValidationException()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test Title");
        conversation.IsDeleted.ShouldBeFalse();

        _restoreRepository.GetByIdIncludingDeletedAsync("conversation", conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var command = new RestoreEntityCommand("conversation", conversation.Id);

        // Act & Assert
        var exception = await Should.ThrowAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        exception.Message.ShouldContain("not soft-deleted");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("message")]
    [InlineData("auditentry")]
    public void Validator_InvalidEntityType_ShouldFail(string entityType)
    {
        // Arrange
        var validator = new RestoreEntityCommandValidator();
        var command = new RestoreEntityCommand(entityType, Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "EntityType");
    }

    [Fact]
    public void Validator_EmptyEntityId_ShouldFail()
    {
        // Arrange
        var validator = new RestoreEntityCommandValidator();
        var command = new RestoreEntityCommand("conversation", Guid.Empty);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "EntityId");
    }

    [Theory]
    [InlineData("conversation")]
    [InlineData("user")]
    [InlineData("systemprompt")]
    [InlineData("Conversation")]
    [InlineData("USER")]
    [InlineData("SystemPrompt")]
    public void Validator_ValidEntityType_ShouldPass(string entityType)
    {
        // Arrange
        var validator = new RestoreEntityCommandValidator();
        var command = new RestoreEntityCommand(entityType, Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_CaseInsensitiveEntityType_ShouldWork()
    {
        // Arrange
        var conversation = CreateDeletedEntity(Conversation.Create(Guid.NewGuid(), "Test Title"));

        _restoreRepository.GetByIdIncludingDeletedAsync("conversation", conversation.Id, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var command = new RestoreEntityCommand("Conversation", conversation.Id);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _restoreRepository.Received(1).Restore(conversation);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EntityNotFound_DoesNotCallRestore()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        _restoreRepository.GetByIdIncludingDeletedAsync("user", entityId, Arg.Any<CancellationToken>())
            .Returns((BaseAuditableEntity<Guid>?)null);

        var command = new RestoreEntityCommand("user", entityId);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));

        _restoreRepository.DidNotReceive().Restore(Arg.Any<BaseAuditableEntity<Guid>>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EntityNotSoftDeleted_DoesNotCallRestore()
    {
        // Arrange
        var user = User.Create("testuser", "test@example.com", Domain.Enums.UserRole.User);
        user.IsDeleted.ShouldBeFalse();

        _restoreRepository.GetByIdIncludingDeletedAsync("user", user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new RestoreEntityCommand("user", user.Id);

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        _restoreRepository.DidNotReceive().Restore(Arg.Any<BaseAuditableEntity<Guid>>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
