using AutoMapper;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;

namespace Mimir.Application.Users.Queries;

public sealed record GetCurrentUserQuery : IQuery<UserDto>;

internal sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    private readonly IUserRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetCurrentUserQueryHandler(
        IUserRepository repository,
        ICurrentUserService currentUserService,
        IMapper mapper)
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

        return _mapper.Map<UserDto>(user);
    }
}
