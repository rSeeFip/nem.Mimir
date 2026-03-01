using System.Text;
using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Enums;
using Mimir.Domain.Events;

namespace Mimir.Application.CodeExecution.Commands;

/// <summary>
/// Command to execute code in a sandboxed environment within a conversation context.
/// </summary>
/// <param name="Language">The programming language of the code (e.g. python, javascript).</param>
/// <param name="Code">The source code to execute.</param>
/// <param name="ConversationId">The conversation in which the code execution takes place.</param>
public sealed record ExecuteCodeCommand(
    string Language,
    string Code,
    Guid ConversationId) : ICommand<CodeExecutionResultDto>;

/// <summary>
/// Represents the result of a sandboxed code execution.
/// </summary>
/// <param name="Stdout">The standard output produced by the executed code.</param>
/// <param name="Stderr">The standard error output produced by the executed code.</param>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="ExecutionTimeMs">The execution duration in milliseconds.</param>
/// <param name="TimedOut">A value indicating whether the execution exceeded the time limit.</param>
public sealed record CodeExecutionResultDto(
    string Stdout,
    string Stderr,
    int ExitCode,
    long ExecutionTimeMs,
    bool TimedOut);

/// <summary>
/// Validates the <see cref="ExecuteCodeCommand"/> ensuring language, code, and conversation ID are valid.
/// </summary>
public sealed class ExecuteCodeCommandValidator : AbstractValidator<ExecuteCodeCommand>
{
    private static readonly string[] AllowedLanguages = ["python", "javascript"];
    private const int MaxCodeSizeBytes = 50 * 1024; // 50KB

    public ExecuteCodeCommandValidator()
    {
        RuleFor(x => x.Language)
            .NotEmpty()
            .Must(lang => AllowedLanguages.Contains(lang.ToLowerInvariant()))
            .WithMessage("Language must be 'python' or 'javascript'.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .Must(code => Encoding.UTF8.GetByteCount(code) <= MaxCodeSizeBytes)
            .WithMessage($"Code must not exceed {MaxCodeSizeBytes / 1024}KB.");

        RuleFor(x => x.ConversationId)
            .NotEmpty();
    }
}

internal sealed class ExecuteCodeCommandHandler : IRequestHandler<ExecuteCodeCommand, CodeExecutionResultDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISandboxService _sandboxService;
    private readonly IUnitOfWork _unitOfWork;

    public ExecuteCodeCommandHandler(
        IConversationRepository conversationRepository,
        ICurrentUserService currentUserService,
        ISandboxService sandboxService,
        IUnitOfWork unitOfWork)
    {
        _conversationRepository = conversationRepository;
        _currentUserService = currentUserService;
        _sandboxService = sandboxService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CodeExecutionResultDto> Handle(
        ExecuteCodeCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        var userGuid = Guid.Parse(userId);

        var conversation = await _conversationRepository.GetByIdAsync(
            request.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Conversation), request.ConversationId);

        // Only owner or admin can execute code in a conversation
        if (conversation.UserId != userGuid && !_currentUserService.Roles.Contains("Admin"))
            throw new ForbiddenAccessException();

        var result = await _sandboxService.ExecuteAsync(
            request.Code, request.Language, cancellationToken);

        // Persist execution result as system message
        var output = FormatExecutionOutput(result, request.Language);
        conversation.AddMessage(MessageRole.System, output);

        // Emit domain event for audit
        conversation.AddDomainEvent(new CodeExecutionEvent(
            request.ConversationId,
            request.Language,
            result.ExitCode,
            result.ExecutionTimeMs,
            result.TimedOut));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CodeExecutionResultDto(
            result.Stdout,
            result.Stderr,
            result.ExitCode,
            result.ExecutionTimeMs,
            result.TimedOut);
    }

    private static string FormatExecutionOutput(CodeExecutionResult result, string language)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Code Execution: {language}]");
        sb.AppendLine($"Exit Code: {result.ExitCode} | Time: {result.ExecutionTimeMs}ms");
        if (result.TimedOut) sb.AppendLine("⚠ Execution timed out");
        if (!string.IsNullOrWhiteSpace(result.Stdout))
        {
            sb.AppendLine("--- stdout ---");
            sb.AppendLine(result.Stdout);
        }
        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            sb.AppendLine("--- stderr ---");
            sb.AppendLine(result.Stderr);
        }
        return sb.ToString();
    }
}
