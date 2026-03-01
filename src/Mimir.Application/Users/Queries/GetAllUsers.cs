using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;

namespace Mimir.Application.Users.Queries;

/// <summary>
/// Query to retrieve a paginated list of all users. Requires administrator privileges.
/// </summary>
/// <param name="PageNumber">The page number to retrieve (default 1).</param>
/// <param name="PageSize">The number of users per page (default 20).</param>
public sealed record GetAllUsersQuery(
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PaginatedList<UserDto>>;

/// <summary>
/// Validates the <see cref="GetAllUsersQuery"/> ensuring pagination parameters are within acceptable ranges.
/// </summary>
public sealed class GetAllUsersQueryValidator : AbstractValidator<GetAllUsersQuery>
{
    public GetAllUsersQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");
    }
}

internal sealed class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, PaginatedList<UserDto>>
{
    private readonly IUserRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetAllUsersQueryHandler(
        IUserRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<PaginatedList<UserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        EnsureAdminRole();

        var (items, totalCount) = await _repository.GetAllAsync(request.PageNumber, request.PageSize, cancellationToken);

        var dtoItems = items.Select(_mapper.MapToUserDto).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PaginatedList<UserDto>(dtoItems, request.PageNumber, totalPages, totalCount);
    }

    private void EnsureAdminRole()
    {
        if (!_currentUserService.IsAuthenticated)
        {
            throw new ForbiddenAccessException("User is not authenticated.");
        }

        if (!_currentUserService.Roles.Contains("Admin"))
        {
            throw new ForbiddenAccessException("Only administrators can perform this action.");
        }
    }
}
