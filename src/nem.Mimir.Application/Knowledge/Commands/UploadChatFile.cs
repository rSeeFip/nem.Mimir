using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Knowledge.Commands;

public sealed record UploadChatFileCommand(
    Guid ConversationId,
    string FileName,
    string ContentType,
    byte[] Content,
    KnowledgeCollectionId? CollectionId) : ICommand<ChatFileUploadDto>;

public sealed class UploadChatFileCommandValidator : AbstractValidator<UploadChatFileCommand>
{
    public UploadChatFileCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .MaximumLength(512).WithMessage("File name must not exceed 512 characters.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.")
            .MaximumLength(256).WithMessage("Content type must not exceed 256 characters.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("File content is required.")
            .Must(c => c.Length > 0).WithMessage("File content is required.")
            .Must(c => c.Length <= 50 * 1024 * 1024).WithMessage("File content must not exceed 50MB.");
    }
}

internal sealed class UploadChatFileCommandHandler
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IKnowledgeCollectionRepository _knowledgeCollectionRepository;
    private readonly IMediaHubClient _mediaHubClient;
    private readonly IKnowledgeIngestionService _knowledgeIngestionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public UploadChatFileCommandHandler(
        IConversationRepository conversationRepository,
        IKnowledgeCollectionRepository knowledgeCollectionRepository,
        IMediaHubClient mediaHubClient,
        IKnowledgeIngestionService knowledgeIngestionService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _conversationRepository = conversationRepository;
        _knowledgeCollectionRepository = knowledgeCollectionRepository;
        _mediaHubClient = mediaHubClient;
        _knowledgeIngestionService = knowledgeIngestionService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ChatFileUploadDto> Handle(UploadChatFileCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        if (conversation.UserId != userId)
        {
            throw new ForbiddenAccessException();
        }

        var upload = await _mediaHubClient
            .UploadAsync(request.FileName, request.ContentType, request.Content, cancellationToken)
            .ConfigureAwait(false);

        await _knowledgeIngestionService
            .IngestDocumentAsync(upload.FileId, request.FileName, upload.Url, cancellationToken)
            .ConfigureAwait(false);

        if (request.CollectionId.HasValue)
        {
            var collection = await _knowledgeCollectionRepository
                .GetByIdForUserAsync(request.CollectionId.Value, userId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("KnowledgeCollection", request.CollectionId.Value);

            collection.AddDocument(upload.FileId, request.FileName, upload.Url, upload.ContentType);
            await _knowledgeCollectionRepository.UpdateAsync(collection, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ChatFileUploadDto(
            upload.FileId,
            request.FileName,
            upload.Url,
            request.CollectionId?.Value,
            "Indexed");
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
