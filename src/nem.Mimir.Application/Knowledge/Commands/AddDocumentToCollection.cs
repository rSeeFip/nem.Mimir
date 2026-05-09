using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Knowledge.Commands;

public sealed record AddDocumentToCollectionCommand(
    KnowledgeCollectionId CollectionId,
    Guid DocumentId,
    string FileName,
    string StorageUrl,
    string? ContentType) : ICommand<KnowledgeCollectionDto>;

public sealed class AddDocumentToCollectionCommandValidator : AbstractValidator<AddDocumentToCollectionCommand>
{
    public AddDocumentToCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty().WithMessage("Collection ID is required.");

        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("Document ID is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .MaximumLength(512).WithMessage("File name must not exceed 512 characters.");

        RuleFor(x => x.StorageUrl)
            .NotEmpty().WithMessage("Storage URL is required.")
            .MaximumLength(4000).WithMessage("Storage URL must not exceed 4000 characters.");
    }
}

internal sealed class AddDocumentToCollectionCommandHandler
{
    private readonly IKnowledgeCollectionRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IKnowledgeIngestionService _knowledgeIngestionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public AddDocumentToCollectionCommandHandler(
        IKnowledgeCollectionRepository repository,
        ICurrentUserService currentUserService,
        IKnowledgeIngestionService knowledgeIngestionService,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _knowledgeIngestionService = knowledgeIngestionService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<KnowledgeCollectionDto> Handle(AddDocumentToCollectionCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var collection = await _repository.GetByIdForUserAsync(request.CollectionId, userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(KnowledgeCollection), request.CollectionId);

        collection.AddDocument(request.DocumentId, request.FileName, request.StorageUrl, request.ContentType);

        await _repository.UpdateAsync(collection, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _knowledgeIngestionService
            .IngestDocumentAsync(request.DocumentId, request.FileName, request.StorageUrl, cancellationToken)
            .ConfigureAwait(false);

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
