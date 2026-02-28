using AutoMapper;
using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;

namespace Mimir.Application.Conversations.Commands;

public sealed record UpdateConversationTitleCommand(
    Guid ConversationId,
    string NewTitle) : ICommand<ConversationDto>;

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
    private readonly IMapper _mapper;

    public UpdateConversationTitleCommandHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper)
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

        return _mapper.Map<ConversationDto>(conversation);
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
