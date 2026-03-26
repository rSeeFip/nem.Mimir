using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Services;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;
using nem.Mimir.Application.Knowledge;

namespace nem.Mimir.Application.Conversations.Commands;

public sealed record AttachFileToConversationCommand(
    Guid ConversationId,
    string FileName,
    string ContentType,
    byte[] Content) : ICommand<ConversationAttachmentDto>;

public sealed class AttachFileToConversationCommandValidator : AbstractValidator<AttachFileToConversationCommand>
{
    public AttachFileToConversationCommandValidator()
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
            .Must(content => content.Length > 0).WithMessage("File content is required.")
            .Must(content => content.Length <= 50 * 1024 * 1024).WithMessage("File content must not exceed 50MB.");
    }
}

internal sealed class AttachFileToConversationCommandHandler
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IKnowledgeCollectionRepository _knowledgeCollectionRepository;
    private readonly IMediaHubClient _mediaHubClient;
    private readonly IKnowledgeIngestionService _knowledgeIngestionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public AttachFileToConversationCommandHandler(
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

    public async Task<ConversationAttachmentDto> Handle(AttachFileToConversationCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        if (conversation.UserId != userId)
            throw new ForbiddenAccessException();

        var upload = await _mediaHubClient
            .UploadAsync(request.FileName, request.ContentType, request.Content, cancellationToken)
            .ConfigureAwait(false);

        await _knowledgeIngestionService
            .IngestDocumentAsync(upload.FileId, request.FileName, upload.Url, cancellationToken)
            .ConfigureAwait(false);

        var collection = await GetOrCreateConversationCollectionAsync(conversation.Id, userId, cancellationToken).ConfigureAwait(false);
        collection.AddDocument(upload.FileId, request.FileName, upload.Url, upload.ContentType);

        await _knowledgeCollectionRepository.UpdateAsync(collection, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ConversationAttachmentDto(
            upload.FileId,
            request.FileName,
            collection.Id.Value,
            "Ready");
    }

    private async Task<KnowledgeCollection> GetOrCreateConversationCollectionAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        var collections = await _knowledgeCollectionRepository.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var expectedName = ConversationKnowledgeCollectionNaming.BuildName(conversationId);

        var existing = collections.FirstOrDefault(collection =>
            string.Equals(collection.Name, expectedName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            return existing;

        var created = KnowledgeCollection.Create(
            userId,
            expectedName,
            $"Conversation-scoped knowledge collection for {conversationId}");

        await _knowledgeCollectionRepository.CreateAsync(created, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return created;
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
