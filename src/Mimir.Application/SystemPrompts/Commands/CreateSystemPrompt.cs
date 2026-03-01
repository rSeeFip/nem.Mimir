using FluentValidation;
using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;

namespace Mimir.Application.SystemPrompts.Commands;

/// <summary>
/// Command to create a new system prompt template.
/// </summary>
/// <param name="Name">The display name of the system prompt.</param>
/// <param name="Template">The template content, which may contain variable placeholders.</param>
/// <param name="Description">A brief description of the system prompt's purpose.</param>
public sealed record CreateSystemPromptCommand(
    string Name,
    string Template,
    string Description) : ICommand<SystemPromptDto>;

/// <summary>
/// Validates the <see cref="CreateSystemPromptCommand"/> ensuring name, template, and description are valid.
/// </summary>
public sealed class CreateSystemPromptCommandValidator : AbstractValidator<CreateSystemPromptCommand>
{
    public CreateSystemPromptCommandValidator()
    {
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

internal sealed class CreateSystemPromptCommandHandler : IRequestHandler<CreateSystemPromptCommand, SystemPromptDto>
{
    private readonly ISystemPromptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public CreateSystemPromptCommandHandler(
        ISystemPromptRepository repository,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<SystemPromptDto> Handle(CreateSystemPromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = SystemPrompt.Create(request.Name, request.Template, request.Description);

        await _repository.CreateAsync(prompt, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.MapToSystemPromptDto(prompt);
    }
}
