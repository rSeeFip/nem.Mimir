using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Memories.Commands;

public sealed record UpdateUserMemoryCommand(
    UserMemoryId Id,
    string Content,
    string? Context) : ICommand;

public sealed class UpdateUserMemoryCommandValidator : AbstractValidator<UpdateUserMemoryCommand>
{
    public UpdateUserMemoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(UserMemoryId.Empty).WithMessage("User memory ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(4000).WithMessage("Content must not exceed 4000 characters.");

        RuleFor(x => x.Context)
            .MaximumLength(1000).WithMessage("Context must not exceed 1000 characters.")
            .When(x => x.Context is not null);
    }
}

internal sealed class UpdateUserMemoryCommandHandler
{
    private readonly IUserMemoryRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserMemoryCommandHandler(
        IUserMemoryRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateUserMemoryCommand request, CancellationToken cancellationToken)
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

        memory.Update(request.Content, request.Context);

        await _repository.UpdateAsync(memory, cancellationToken).ConfigureAwait(false);
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
