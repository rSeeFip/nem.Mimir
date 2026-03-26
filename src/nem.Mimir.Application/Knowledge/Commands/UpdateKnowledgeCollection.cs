using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Knowledge.Commands;

public sealed record UpdateKnowledgeCollectionCommand(
    KnowledgeCollectionId Id,
    string Name,
    string? Description) : ICommand<KnowledgeCollectionDto>;

public sealed class UpdateKnowledgeCollectionCommandValidator : AbstractValidator<UpdateKnowledgeCollectionCommand>
{
    public UpdateKnowledgeCollectionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Collection ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Collection name is required.")
            .MaximumLength(200).WithMessage("Collection name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Collection description must not exceed 2000 characters.");
    }
}

internal sealed class UpdateKnowledgeCollectionCommandHandler
{
    private readonly IKnowledgeCollectionRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public UpdateKnowledgeCollectionCommandHandler(
        IKnowledgeCollectionRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<KnowledgeCollectionDto> Handle(UpdateKnowledgeCollectionCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var collection = await _repository.GetByIdForUserAsync(request.Id, userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(KnowledgeCollection), request.Id);

        collection.Update(request.Name, request.Description);

        await _repository.UpdateAsync(collection, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return _mapper.MapToKnowledgeCollectionDto(collection);
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
