using nem.Mimir.Application.Common.Mappings;
using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Conversations.Queries;

/// <summary>
/// Query to retrieve a paginated list of messages for a specific conversation.
/// </summary>
/// <param name="ConversationId">The unique identifier of the conversation.</param>
/// <param name="PageNumber">The page number to retrieve (1-based).</param>
/// <param name="PageSize">The number of messages per page.</param>
public sealed record GetConversationMessagesQuery(
    Guid ConversationId,
    int PageNumber,
    int PageSize) : IQuery<PaginatedList<MessageDto>>;

/// <summary>
/// Validates the <see cref="GetConversationMessagesQuery"/> ensuring pagination parameters are within acceptable ranges.
/// </summary>
public sealed class GetConversationMessagesQueryValidator : AbstractValidator<GetConversationMessagesQuery>
{
    public GetConversationMessagesQueryValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

internal sealed class GetConversationMessagesQueryHandler
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetConversationMessagesQueryHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<PaginatedList<MessageDto>> Handle(GetConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetByIdAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        EnsureOwnership(conversation);

        var result = await _repository.GetMessagesAsync(request.ConversationId, request.PageNumber, request.PageSize, cancellationToken);

        var dtoItems = result.Items.Select(_mapper.MapToMessageDto).ToList();

        return new PaginatedList<MessageDto>(dtoItems, result.PageNumber, result.TotalPages, result.TotalCount);
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
