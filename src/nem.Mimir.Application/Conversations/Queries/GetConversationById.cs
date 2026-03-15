using nem.Mimir.Application.Common.Mappings;
using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Conversations.Queries;

/// <summary>
/// Query to retrieve a single conversation by its unique identifier, including its messages.
/// </summary>
/// <param name="ConversationId">The unique identifier of the conversation to retrieve.</param>
public sealed record GetConversationByIdQuery(Guid ConversationId) : IQuery<ConversationDto>;

internal sealed class GetConversationByIdQueryHandler : IRequestHandler<GetConversationByIdQuery, ConversationDto>
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetConversationByIdQueryHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<ConversationDto> Handle(GetConversationByIdQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetWithMessagesAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        EnsureOwnership(conversation);

        return _mapper.MapToConversationDto(conversation);
    }

    private void EnsureOwnership(Conversation conversation)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        if (conversation.UserId != userGuid)
        {
            throw new ForbiddenAccessException();
        }
}
}
