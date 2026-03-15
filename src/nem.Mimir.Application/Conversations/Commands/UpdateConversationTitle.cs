using nem.Mimir.Application.Common.Mappings;
using FluentValidation;
using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Conversations.Commands;

/// <summary>
/// Command to update the title of an existing conversation.
/// </summary>
/// <param name="ConversationId">The unique identifier of the conversation to update.</param>
/// <param name="NewTitle">The new title for the conversation.</param>
public sealed record UpdateConversationTitleCommand(
    Guid ConversationId,
    string NewTitle) : ICommand<ConversationDto>;

/// <summary>
/// Validates the <see cref="UpdateConversationTitleCommand"/> ensuring the conversation ID and new title are valid.
/// </summary>
public sealed class UpdateConversationTitleCommandValidator : AbstractValidator<UpdateConversationTitleCommand>
{
    public UpdateConversationTitleCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.NewTitle)
            .NotEmpty().WithMessage("New title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");
    }
}

internal sealed class UpdateConversationTitleCommandHandler : IRequestHandler<UpdateConversationTitleCommand, ConversationDto>
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public UpdateConversationTitleCommandHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ConversationDto> Handle(UpdateConversationTitleCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetByIdAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        EnsureOwnership(conversation);

        conversation.UpdateTitle(request.NewTitle);

        await _repository.UpdateAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.MapToConversationDto(conversation);
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
