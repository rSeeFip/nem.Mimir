using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Models.Commands;

public sealed record DeleteModelProfileCommand(ModelProfileId Id) : ICommand;

public sealed class DeleteModelProfileCommandValidator : AbstractValidator<DeleteModelProfileCommand>
{
    public DeleteModelProfileCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(ModelProfileId.Empty).WithMessage("Model profile ID is required.");
    }
}

internal sealed class DeleteModelProfileCommandHandler
{
    private readonly IModelProfileRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteModelProfileCommandHandler(
        IModelProfileRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteModelProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var profile = await _repository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(ModelProfile), request.Id);

        if (profile.UserId != userId)
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
