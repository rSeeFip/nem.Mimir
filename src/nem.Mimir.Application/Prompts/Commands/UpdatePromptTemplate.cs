using FluentValidation;
using nem.Contracts.Identity;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Prompts.Commands;

public sealed record UpdatePromptTemplateCommand(
    PromptTemplateId Id,
    string Title,
    string Command,
    string Content,
    IReadOnlyList<string>? Tags) : ICommand;

public sealed class UpdatePromptTemplateCommandValidator : AbstractValidator<UpdatePromptTemplateCommand>
{
    public UpdatePromptTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(PromptTemplateId.Empty).WithMessage("Prompt template ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Prompt template title is required.")
            .MaximumLength(200).WithMessage("Prompt template title must not exceed 200 characters.");

        RuleFor(x => x.Command)
            .NotEmpty().WithMessage("Prompt template command is required.")
            .Matches("^/[a-z0-9-]+$").WithMessage("Prompt template command must match '/command-name'.")
            .MaximumLength(100).WithMessage("Prompt template command must not exceed 100 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Prompt template content is required.")
            .MaximumLength(20000).WithMessage("Prompt template content must not exceed 20000 characters.");

        RuleForEach(x => x.Tags)
            .MaximumLength(ConversationTag.MaxLength).WithMessage($"Tag cannot exceed {ConversationTag.MaxLength} characters.");
    }
}

internal sealed class UpdatePromptTemplateCommandHandler
{
    private readonly IPromptTemplateRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePromptTemplateCommandHandler(
        IPromptTemplateRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdatePromptTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var promptTemplate = await _repository
            .GetByIdForUserAsync(request.Id, userId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PromptTemplate), request.Id);

        var existingByCommand = await _repository
            .GetByCommandForUserAsync(request.Command, userId, cancellationToken)
            .ConfigureAwait(false);

        if (existingByCommand is not null && existingByCommand.Id != request.Id)
        {
            throw new ConflictException($"A prompt template with command '{request.Command}' already exists.");
        }

        promptTemplate.Update(request.Title, request.Command, request.Content, request.Tags);

        await _repository.UpdateAsync(promptTemplate, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Guid ResolveCurrentUserId(ICurrentUserService currentUserService)
    {
        var userId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new ForbiddenAccessException("Current user identifier is invalid.");

        return parsedUserId;
    }
}
