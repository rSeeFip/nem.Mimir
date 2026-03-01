using AutoMapper;
using FluentValidation;
using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;

namespace Mimir.Application.SystemPrompts.Commands;

public sealed record CreateSystemPromptCommand(
    string Name,
    string Template,
    string Description) : ICommand<SystemPromptDto>;

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
    private readonly IMapper _mapper;

    public CreateSystemPromptCommandHandler(
        ISystemPromptRepository repository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
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

        return _mapper.Map<SystemPromptDto>(prompt);
    }
}
