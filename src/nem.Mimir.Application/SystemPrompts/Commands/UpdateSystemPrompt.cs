using FluentValidation;
using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.SystemPrompts.Commands;

/// <summary>
/// Command to update an existing system prompt's name, template, and description.
/// </summary>
/// <param name="Id">The unique identifier of the system prompt to update.</param>
/// <param name="Name">The updated display name.</param>
/// <param name="Template">The updated template content.</param>
/// <param name="Description">The updated description.</param>
public sealed record UpdateSystemPromptCommand(
    Guid Id,
    string Name,
    string Template,
    string Description) : ICommand;

/// <summary>
/// Validates the <see cref="UpdateSystemPromptCommand"/> ensuring all fields are valid and within length limits.
/// </summary>
public sealed class UpdateSystemPromptCommandValidator : AbstractValidator<UpdateSystemPromptCommand>
{
    public UpdateSystemPromptCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("System prompt ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Template)
            .NotEmpty().WithMessage("Template is required.")
            .MaximumLength(10000).WithMessage("Template must not exceed 10000 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");
    }
}

internal sealed class UpdateSystemPromptCommandHandler : IRequestHandler<UpdateSystemPromptCommand>
{
    private readonly ISystemPromptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSystemPromptCommandHandler(
        ISystemPromptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateSystemPromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SystemPrompt), request.Id);

        prompt.UpdateName(request.Name);
        prompt.UpdateTemplate(request.Template);
        prompt.UpdateDescription(request.Description);

        await _repository.UpdateAsync(prompt, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
