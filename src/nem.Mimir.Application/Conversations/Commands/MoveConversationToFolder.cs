using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Conversations.Commands;

public sealed record MoveConversationToFolderCommand(Guid ConversationId, Guid? FolderId) : ICommand;

public sealed class MoveConversationToFolderCommandValidator : AbstractValidator<MoveConversationToFolderCommand>
{
    public MoveConversationToFolderCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");
    }
}

internal sealed class MoveConversationToFolderCommandHandler
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public MoveConversationToFolderCommandHandler(
        IConversationRepository conversationRepository,
        IFolderRepository folderRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _conversationRepository = conversationRepository;
        _folderRepository = folderRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(MoveConversationToFolderCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        EnsureConversationOwnership(conversation);

        if (request.FolderId.HasValue)
        {
            var folder = await _folderRepository.GetByIdAsync(FolderId.From(request.FolderId.Value), cancellationToken)
                ?? throw new NotFoundException(nameof(Folder), request.FolderId.Value);

            EnsureFolderOwnership(folder);
        }

        conversation.MoveToFolder(request.FolderId);

        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void EnsureConversationOwnership(Conversation conversation)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (conversation.UserId != Guid.Parse(userId))
            throw new ForbiddenAccessException();
    }

    private void EnsureFolderOwnership(Folder folder)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (folder.UserId != Guid.Parse(userId))
            throw new ForbiddenAccessException();
    }
}
