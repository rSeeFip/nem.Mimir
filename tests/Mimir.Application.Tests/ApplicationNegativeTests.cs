using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Conversations.Commands;
using Mimir.Application.SystemPrompts.Commands;
using Mimir.Application.Users.Commands;
using Mimir.Application.Plugins.Commands;
using Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests;

/// <summary>
/// Comprehensive negative tests for Application layer command handlers and validators.
/// Covers error paths, boundary conditions, invalid inputs, and failure modes.
/// </summary>
public sealed class ApplicationNegativeTests
{
    // ── Shared mocks ────────────────────────────────────────────────

    private readonly IConversationRepository _conversationRepo = Substitute.For<IConversationRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly ISystemPromptRepository _systemPromptRepo = Substitute.For<ISystemPromptRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly MimirMapper _mapper = new();

    // ══════════════════════════════════════════════════════════════════
    // SendMessageCommand Validator — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void SendMessageValidator_EmptyConversationId_ShouldFail()
    {
        var validator = new SendMessageCommandValidator();
        var command = new SendMessageCommand(Guid.Empty, "Hello", null);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ConversationId");
    }

    [Fact]
    public void SendMessageValidator_EmptyContent_ShouldFail()
    {
        var validator = new SendMessageCommandValidator();
        var command = new SendMessageCommand(Guid.NewGuid(), "", null);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Content");
    }

    [Fact]
    public void SendMessageValidator_ContentExceeds32000Chars_ShouldFail()
    {
        var validator = new SendMessageCommandValidator();
        var command = new SendMessageCommand(Guid.NewGuid(), new string('x', 32_001), null);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Content");
    }

    [Fact]
    public void SendMessageValidator_WhitespaceContent_ShouldFail()
    {
        var validator = new SendMessageCommandValidator();
        var command = new SendMessageCommand(Guid.NewGuid(), "   ", null);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Content");
    }

    [Fact]
    public void SendMessageValidator_ContentExactlyAtLimit_ShouldPass()
    {
        var validator = new SendMessageCommandValidator();
        var command = new SendMessageCommand(Guid.NewGuid(), new string('x', 32_000), null);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    // ══════════════════════════════════════════════════════════════════
    // CreateUserCommand Validator — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateUserValidator_EmptyEmail_ShouldFail()
    {
        var validator = new CreateUserCommandValidator();
        var command = new CreateUserCommand("", "Display Name");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void CreateUserValidator_InvalidEmailFormat_ShouldFail()
    {
        var validator = new CreateUserCommandValidator();
        var command = new CreateUserCommand("not-an-email", "Display Name");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void CreateUserValidator_EmptyDisplayName_ShouldFail()
    {
        var validator = new CreateUserCommandValidator();
        var command = new CreateUserCommand("test@example.com", "");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "DisplayName");
    }

    [Fact]
    public void CreateUserValidator_DisplayNameTooLong_ShouldFail()
    {
        var validator = new CreateUserCommandValidator();
        var command = new CreateUserCommand("test@example.com", new string('a', 101));

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "DisplayName");
    }

    [Fact]
    public void CreateUserValidator_DisplayNameAtExactLimit_ShouldPass()
    {
        var validator = new CreateUserCommandValidator();
        var command = new CreateUserCommand("test@example.com", new string('a', 100));

        var result = validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    // ══════════════════════════════════════════════════════════════════
    // UpdateSystemPromptCommand Validator — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void UpdateSystemPromptValidator_EmptyId_ShouldFail()
    {
        var validator = new UpdateSystemPromptCommandValidator();
        var command = new UpdateSystemPromptCommand(Guid.Empty, "Name", "Template", "Desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void UpdateSystemPromptValidator_EmptyName_ShouldFail()
    {
        var validator = new UpdateSystemPromptCommandValidator();
        var command = new UpdateSystemPromptCommand(Guid.NewGuid(), "", "Template", "Desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void UpdateSystemPromptValidator_NameTooLong_ShouldFail()
    {
        var validator = new UpdateSystemPromptCommandValidator();
        var command = new UpdateSystemPromptCommand(Guid.NewGuid(), new string('n', 201), "Template", "Desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void UpdateSystemPromptValidator_EmptyTemplate_ShouldFail()
    {
        var validator = new UpdateSystemPromptCommandValidator();
        var command = new UpdateSystemPromptCommand(Guid.NewGuid(), "Name", "", "Desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Template");
    }

    [Fact]
    public void UpdateSystemPromptValidator_TemplateTooLong_ShouldFail()
    {
        var validator = new UpdateSystemPromptCommandValidator();
        var command = new UpdateSystemPromptCommand(Guid.NewGuid(), "Name", new string('t', 10_001), "Desc");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Template");
    }

    [Fact]
    public void UpdateSystemPromptValidator_DescriptionTooLong_ShouldFail()
    {
        var validator = new UpdateSystemPromptCommandValidator();
        var command = new UpdateSystemPromptCommand(Guid.NewGuid(), "Name", "Template", new string('d', 1_001));

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Description");
    }

    // ══════════════════════════════════════════════════════════════════
    // DeleteConversationCommand Handler — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteConversationHandler_ConversationNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _conversationRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        var handler = new DeleteConversationCommandHandler(_conversationRepo, _currentUserService, _unitOfWork);
        var command = new DeleteConversationCommand(Guid.NewGuid());

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteConversationHandler_UserNotAuthenticated_ThrowsForbiddenAccessException()
    {
        // Arrange
        var conversation = Conversation.Create(Guid.NewGuid(), "Test");
        _conversationRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(conversation);
        _currentUserService.UserId.Returns((string?)null);

        var handler = new DeleteConversationCommandHandler(_conversationRepo, _currentUserService, _unitOfWork);
        var command = new DeleteConversationCommand(conversation.Id);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteConversationHandler_WrongOwner_ThrowsForbiddenAccessException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var conversation = Conversation.Create(ownerId, "Test");

        _conversationRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(conversation);
        _currentUserService.UserId.Returns(differentUserId.ToString());

        var handler = new DeleteConversationCommandHandler(_conversationRepo, _currentUserService, _unitOfWork);
        var command = new DeleteConversationCommand(conversation.Id);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void DeleteConversationValidator_EmptyConversationId_ShouldFail()
    {
        var validator = new DeleteConversationCommandValidator();
        var command = new DeleteConversationCommand(Guid.Empty);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ConversationId");
    }

    // ══════════════════════════════════════════════════════════════════
    // DeactivateUserCommand Handler — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeactivateUserHandler_NotAuthenticated_ThrowsForbiddenAccessException()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(false);

        var handler = new DeactivateUserCommandHandler(_userRepo, _currentUserService, _unitOfWork);
        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task DeactivateUserHandler_NotAdmin_ThrowsForbiddenAccessException()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "User" });

        var handler = new DeactivateUserCommandHandler(_userRepo, _currentUserService, _unitOfWork);
        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task DeactivateUserHandler_UserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.Roles.Returns(new List<string> { "Admin" });
        _userRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new DeactivateUserCommandHandler(_userRepo, _currentUserService, _unitOfWork);
        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public void DeactivateUserValidator_EmptyUserId_ShouldFail()
    {
        var validator = new DeactivateUserCommandValidator();
        var command = new DeactivateUserCommand(Guid.Empty);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "UserId");
    }

    // ══════════════════════════════════════════════════════════════════
    // ExecutePluginCommand Validator — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void ExecutePluginValidator_EmptyPluginId_ShouldFail()
    {
        var validator = new ExecutePluginCommandValidator();
        var command = new ExecutePluginCommand("", "user-1", new Dictionary<string, object>());

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PluginId");
    }

    [Fact]
    public void ExecutePluginValidator_EmptyUserId_ShouldFail()
    {
        var validator = new ExecutePluginCommandValidator();
        var command = new ExecutePluginCommand("plugin-1", "", new Dictionary<string, object>());

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "UserId");
    }

    // ══════════════════════════════════════════════════════════════════
    // ArchiveConversationCommand — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void ArchiveConversationValidator_EmptyId_ShouldFail()
    {
        var validator = new ArchiveConversationCommandValidator();
        var command = new ArchiveConversationCommand(Guid.Empty);

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ConversationId");
    }

    [Fact]
    public async Task ArchiveConversationHandler_ConversationNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _conversationRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        var handler = new ArchiveConversationCommandHandler(_conversationRepo, _currentUserService, _unitOfWork);
        var command = new ArchiveConversationCommand(Guid.NewGuid());

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    // ══════════════════════════════════════════════════════════════════
    // UpdateSystemPromptCommand Handler — negative cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateSystemPromptHandler_PromptNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _systemPromptRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((SystemPrompt?)null);

        var handler = new UpdateSystemPromptCommandHandler(_systemPromptRepo, _unitOfWork);
        var command = new UpdateSystemPromptCommand(Guid.NewGuid(), "Name", "Template", "Desc");

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(command, CancellationToken.None));
    }
}
