using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;

using ValidationException = Mimir.Application.Common.Exceptions.ValidationException;

namespace Mimir.Application.Admin.Commands;

/// <summary>
/// Command to restore a soft-deleted entity by type and identifier.
/// </summary>
/// <param name="EntityType">The type of entity to restore (e.g. conversation, user, systemprompt).</param>
/// <param name="EntityId">The unique identifier of the entity to restore.</param>
public sealed record RestoreEntityCommand(string EntityType, Guid EntityId) : ICommand;

/// <summary>
/// Validates the <see cref="RestoreEntityCommand"/> ensuring entity type and ID are provided and valid.
/// </summary>
public sealed class RestoreEntityCommandValidator : AbstractValidator<RestoreEntityCommand>
{
    private static readonly HashSet<string> ValidEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "conversation",
        "user",
        "systemprompt",
    };

    public RestoreEntityCommandValidator()
    {
        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("Entity type is required.")
            .Must(type => ValidEntityTypes.Contains(type))
            .WithMessage("Invalid entity type. Valid types are: conversation, user, systemprompt.");

        RuleFor(x => x.EntityId)
            .NotEmpty().WithMessage("Entity ID is required.");
    }
}

internal sealed class RestoreEntityCommandHandler : IRequestHandler<RestoreEntityCommand>
{
    private readonly IEntityRestoreRepository _restoreRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RestoreEntityCommandHandler(
        IEntityRestoreRepository restoreRepository,
        IUnitOfWork unitOfWork)
    {
        _restoreRepository = restoreRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RestoreEntityCommand request, CancellationToken cancellationToken)
    {
        var entityType = request.EntityType.ToLowerInvariant();

        var entity = await _restoreRepository.GetByIdIncludingDeletedAsync(
            entityType, request.EntityId, cancellationToken)
            ?? throw new NotFoundException(request.EntityType, request.EntityId);

        bool isDeleted = (bool)entity.GetType().GetProperty("IsDeleted")!.GetValue(entity)!;
        if (!isDeleted)
        {
            throw new ValidationException(
                $"Entity \"{request.EntityType}\" ({request.EntityId}) is not soft-deleted and cannot be restored.");
        }
        _restoreRepository.Restore(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
