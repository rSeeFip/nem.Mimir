using AutoMapper;
using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;

namespace Mimir.Application.Conversations.Queries;

public sealed record GetConversationsByUserQuery(
    int PageNumber,
    int PageSize) : IQuery<PaginatedList<ConversationListDto>>;

public sealed class GetConversationsByUserQueryValidator : AbstractValidator<GetConversationsByUserQuery>
{
    public GetConversationsByUserQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");
    }
}

internal sealed class GetConversationsByUserQueryHandler : IRequestHandler<GetConversationsByUserQuery, PaginatedList<ConversationListDto>>
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetConversationsByUserQueryHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<PaginatedList<ConversationListDto>> Handle(GetConversationsByUserQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        var userGuid = Guid.Parse(userId);

        var result = await _repository.GetByUserIdAsync(userGuid, request.PageNumber, request.PageSize, cancellationToken);

        var dtoItems = _mapper.Map<IReadOnlyCollection<ConversationListDto>>(result.Items);

        return new PaginatedList<ConversationListDto>(dtoItems, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
