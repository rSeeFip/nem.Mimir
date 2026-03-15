using FluentValidation;
using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Conversations.Commands;

/// <summary>
/// Command to archive a conversation by its identifier.
/// </summary>
/// <param name="ConversationId">The unique identifier of the conversation to archive.</param>
public sealed record ArchiveConversationCommand(Guid ConversationId) : ICommand;

/// <summary>
/// Validates the <see cref="ArchiveConversationCommand"/> ensuring the conversation ID is provided.
/// </summary>
public sealed class ArchiveConversationCommandValidator : AbstractValidator<ArchiveConversationCommand>
{
    public ArchiveConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");
    }
}

internal sealed class ArchiveConversationCommandHandler : IRequestHandler<ArchiveConversationCommand>
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveConversationCommandHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ArchiveConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetByIdAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        EnsureOwnership(conversation);

        conversation.Archive();

        await _repository.UpdateAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void EnsureOwnership(Conversation conversation)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (conversation.UserId != Guid.Parse(userId))
        {
            throw new ForbiddenAccessException();
        }
    }
}
