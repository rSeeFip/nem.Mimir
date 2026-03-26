using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Memories.Commands;

public sealed record DeleteUserMemoryCommand(UserMemoryId Id) : ICommand;

public sealed class DeleteUserMemoryCommandValidator : AbstractValidator<DeleteUserMemoryCommand>
{
    public DeleteUserMemoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(UserMemoryId.Empty).WithMessage("User memory ID is required.");
    }
}

internal sealed class DeleteUserMemoryCommandHandler
{
    private readonly IUserMemoryRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserMemoryCommandHandler(
        IUserMemoryRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteUserMemoryCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var memory = await _repository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(UserMemory), request.Id);

        if (memory.UserId != userId)
        {
            throw new ForbiddenAccessException();
        }

        await _repository.DeleteAsync(request.Id, userId, cancellationToken).ConfigureAwait(false);
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
