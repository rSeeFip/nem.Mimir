using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;

namespace Mimir.Application.Conversations.Queries;

/// <summary>
/// Query to retrieve a paginated list of conversations for the currently authenticated user.
/// </summary>
/// <param name="PageNumber">The page number to retrieve (1-based).</param>
/// <param name="PageSize">The number of conversations per page.</param>
public sealed record GetConversationsByUserQuery(
    int PageNumber,
    int PageSize) : IQuery<PaginatedList<ConversationListDto>>;

/// <summary>
/// Validates the <see cref="GetConversationsByUserQuery"/> ensuring pagination parameters are within acceptable ranges.
/// </summary>
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
    private readonly MimirMapper _mapper;

    public GetConversationsByUserQueryHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
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

        var dtoItems = result.Items.Select(_mapper.MapToConversationListDto).ToList();

        return new PaginatedList<ConversationListDto>(dtoItems, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
