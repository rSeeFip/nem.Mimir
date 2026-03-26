using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Knowledge.Commands;

public sealed record RemoveDocumentCommand(
    KnowledgeCollectionId CollectionId,
    Guid DocumentId) : ICommand;

public sealed class RemoveDocumentCommandValidator : AbstractValidator<RemoveDocumentCommand>
{
    public RemoveDocumentCommandValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty().WithMessage("Collection ID is required.");

        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("Document ID is required.");
    }
}

internal sealed class RemoveDocumentCommandHandler
{
    private readonly IKnowledgeCollectionRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveDocumentCommandHandler(
        IKnowledgeCollectionRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RemoveDocumentCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var collection = await _repository.GetByIdForUserAsync(request.CollectionId, userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(KnowledgeCollection), request.CollectionId);

        var removed = collection.RemoveDocument(request.DocumentId);
        if (!removed)
        {
            throw new NotFoundException("KnowledgeDocument", request.DocumentId);
        }

        await _repository.UpdateAsync(collection, cancellationToken).ConfigureAwait(false);
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
