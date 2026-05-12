using nem.Mimir.Application.CodeExecution.Commands;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Events;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.CodeExecution.Commands;

public sealed class ExecuteCodeTests
{
    private readonly IConversationRepository _conversationRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISandboxService _sandboxService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ExecuteCodeCommandHandler _handler;

    public ExecuteCodeTests()
    {
        _conversationRepository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _sandboxService = Substitute.For<ISandboxService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _handler = new ExecuteCodeCommandHandler(
            _conversationRepository, _currentUserService, _sandboxService, _unitOfWork);
    }

    // --- Happy path tests ---

    [Fact]
    public async Task Handle_SuccessfulPythonExecution_ReturnsResultAndPersistsMessageAndEmitsEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _currentUserService.Roles.Returns(new List<string>());

        var conversation = Conversation.Create(userId, "Test Chat");
        SetConversationId(conversation, conversationId);

        _conversationRepository.GetByIdAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var executionResult = new CodeExecutionResult("Hello World\n", "", 0, 150, false);
        _sandboxService.ExecuteAsync("print('Hello World')", "python", Arg.Any<CancellationToken>())
            .Returns(executionResult);

        var command = new ExecuteCodeCommand("python", "print('Hello World')", conversationId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Stdout.ShouldBe("Hello World\n");
        result.Stderr.ShouldBe("");
        result.ExitCode.ShouldBe(0);
        result.ExecutionTimeMs.ShouldBe(150);
        result.TimedOut.ShouldBeFalse();

        // Verify system message was persisted
        conversation.Messages.Count.ShouldBe(1);
        conversation.Messages.ShouldContain(m => m.Role == MessageRole.System);

        // Verify domain event emitted
        conversation.DomainEvents.ShouldContain(e => e is CodeExecutionEvent);
        var domainEvent = conversation.DomainEvents.OfType<CodeExecutionEvent>().First();
        domainEvent.ConversationId.ShouldBe(conversationId);
        domainEvent.Language.ShouldBe("python");
        domainEvent.ExitCode.ShouldBe(0);
        domainEvent.ExecutionTimeMs.ShouldBe(150);
        domainEvent.TimedOut.ShouldBeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulJavaScriptExecution_ReturnsResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _currentUserService.Roles.Returns(new List<string>());

        var conversation = Conversation.Create(userId, "JS Chat");
        SetConversationId(conversation, conversationId);

        _conversationRepository.GetByIdAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var executionResult = new CodeExecutionResult("42\n", "", 0, 80, false);
        _sandboxService.ExecuteAsync("console.log(42)", "javascript", Arg.Any<CancellationToken>())
            .Returns(executionResult);

        var command = new ExecuteCodeCommand("javascript", "console.log(42)", conversationId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Stdout.ShouldBe("42\n");
        result.ExitCode.ShouldBe(0);
        result.ExecutionTimeMs.ShouldBe(80);
        result.TimedOut.ShouldBeFalse();
    }

    // --- Validation tests ---

    [Fact]
    public void Validator_EmptyLanguage_ShouldFail()
    {
        // Arrange
        var validator = new ExecuteCodeCommandValidator();
        var command = new ExecuteCodeCommand("", "print('hi')", Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Language");
    }

    [Fact]
    public void Validator_InvalidLanguage_ShouldFail()
    {
        // Arrange
        var validator = new ExecuteCodeCommandValidator();
        var command = new ExecuteCodeCommand("ruby", "puts 'hi'", Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Language");
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("python") && e.ErrorMessage.Contains("javascript"));
    }

    [Fact]
    public void Validator_CodeExceeding50KB_ShouldFail()
    {
        // Arrange
        var validator = new ExecuteCodeCommandValidator();
        var largeCode = new string('x', 51 * 1024); // 51KB exceeds 50KB limit
        var command = new ExecuteCodeCommand("python", largeCode, Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Code");
    }

    [Fact]
    public void Validator_EmptyCode_ShouldFail()
    {
        // Arrange
        var validator = new ExecuteCodeCommandValidator();
        var command = new ExecuteCodeCommand("python", "", Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Code");
    }

    [Fact]
    public void Validator_EmptyConversationId_ShouldFail()
    {
        // Arrange
        var validator = new ExecuteCodeCommandValidator();
        var command = new ExecuteCodeCommand("python", "print('hi')", Guid.Empty);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ConversationId");
    }

    [Fact]
    public void Validator_ValidPythonCommand_ShouldPass()
    {
        // Arrange
        var validator = new ExecuteCodeCommandValidator();
        var command = new ExecuteCodeCommand("python", "print('hello')", Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_ValidJavaScriptCommand_ShouldPass()
    {
        // Arrange
        var validator = new ExecuteCodeCommandValidator();
        var command = new ExecuteCodeCommand("javascript", "console.log('hi')", Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_LanguageCaseInsensitive_ShouldPass()
    {
        // Arrange
        var validator = new ExecuteCodeCommandValidator();
        var command = new ExecuteCodeCommand("Python", "print('hi')", Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    // --- Authorization tests ---

    [Fact]
    public async Task Handle_UnauthenticatedUser_ThrowsForbiddenAccessException()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        var command = new ExecuteCodeCommand("python", "print('hi')", Guid.NewGuid());

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NonOwnerNonAdmin_ThrowsForbiddenAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _currentUserService.Roles.Returns(new List<string>());

        var conversation = Conversation.Create(otherUserId, "Other User Chat");

        _conversationRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        var command = new ExecuteCodeCommand("python", "print('hi')", conversation.Id);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    // --- Not found test ---

    [Fact]
    public async Task Handle_ConversationNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        _conversationRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        var command = new ExecuteCodeCommand("python", "print('hi')", Guid.NewGuid());

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    // --- Timeout test ---

    [Fact]
    public async Task Handle_SandboxTimesOut_ResultReflectsTimeout()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _currentUserService.Roles.Returns(new List<string>());

        var conversation = Conversation.Create(userId, "Timeout Chat");
        SetConversationId(conversation, conversationId);

        _conversationRepository.GetByIdAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var executionResult = new CodeExecutionResult("", "", 137, 30000, true);
        _sandboxService.ExecuteAsync(Arg.Any<string>(), "python", Arg.Any<CancellationToken>())
            .Returns(executionResult);

        var command = new ExecuteCodeCommand("python", "while True: pass", conversationId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TimedOut.ShouldBeTrue();
        result.ExitCode.ShouldBe(137);
        result.ExecutionTimeMs.ShouldBe(30000);

        // Verify system message mentions timeout
        var systemMessage = conversation.Messages.First(m => m.Role == MessageRole.System);
        systemMessage.Content.ShouldContain("timed out");
    }

    // --- Admin access test ---

    [Fact]
    public async Task Handle_AdminCanExecuteInAnyConversation()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        _currentUserService.UserId.Returns(adminUserId.ToString());
        _currentUserService.Roles.Returns(new List<string> { "Admin" });

        var conversation = Conversation.Create(ownerUserId, "Other User Chat");
        SetConversationId(conversation, conversationId);

        _conversationRepository.GetByIdAsync(conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var executionResult = new CodeExecutionResult("admin output\n", "", 0, 100, false);
        _sandboxService.ExecuteAsync("print('admin')", "python", Arg.Any<CancellationToken>())
            .Returns(executionResult);

        var command = new ExecuteCodeCommand("python", "print('admin')", conversationId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Stdout.ShouldBe("admin output\n");
        result.ExitCode.ShouldBe(0);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // --- Helper methods ---

    private static void SetConversationId(Conversation conversation, Guid id)
    {
        // Use reflection to set the Id for testing purposes
        var prop = typeof(Conversation).BaseType!.BaseType!.GetProperty("Id");
        prop!.SetValue(conversation, id);
    }
}
