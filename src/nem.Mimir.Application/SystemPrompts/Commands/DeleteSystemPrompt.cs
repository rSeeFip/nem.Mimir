using FluentValidation;
using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.SystemPrompts.Commands;

/// <summary>
/// Command to delete an existing system prompt by its identifier.
/// </summary>
/// <param name="Id">The unique identifier of the system prompt to delete.</param>
public sealed record DeleteSystemPromptCommand(Guid Id) : ICommand;

/// <summary>
/// Validates the <see cref="DeleteSystemPromptCommand"/> ensuring the system prompt ID is provided.
/// </summary>
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
