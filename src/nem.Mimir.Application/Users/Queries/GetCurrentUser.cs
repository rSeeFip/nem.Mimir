using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Users.Queries;

/// <summary>
/// Query to retrieve the profile of the currently authenticated user.
/// </summary>
public sealed record GetCurrentUserQuery : IQuery<UserDto>;

internal sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    private readonly IUserRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetCurrentUserQueryHandler(
        IUserRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        var userGuid = Guid.Parse(userId);

        var user = await _repository.GetByIdAsync(userGuid, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userGuid);

        return _mapper.MapToUserDto(user);
    }
}
