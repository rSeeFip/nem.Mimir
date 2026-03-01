using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Entities;

namespace Mimir.Application.SystemPrompts.Commands;

public sealed record UpdateSystemPromptCommand(
    Guid Id,
    string Name,
    string Template,
    string Description) : ICommand;

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
