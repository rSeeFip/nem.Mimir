using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Entities;

namespace Mimir.Application.SystemPrompts.Commands;

public sealed record DeleteSystemPromptCommand(Guid Id) : ICommand;

public sealed class DeleteSystemPromptCommandValidator : AbstractValidator<DeleteSystemPromptCommand>
{
    public DeleteSystemPromptCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("System prompt ID is required.");
    }
}

internal sealed class DeleteSystemPromptCommandHandler : IRequestHandler<DeleteSystemPromptCommand>
{
    private readonly ISystemPromptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSystemPromptCommandHandler(
        ISystemPromptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteSystemPromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SystemPrompt), request.Id);

        await _repository.DeleteAsync(request.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
