using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Users.Queries;

/// <summary>
/// Query to retrieve a user by their unique identifier.
/// </summary>
/// <param name="UserId">The unique identifier of the user to retrieve.</param>
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserDto>;

internal sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly IUserRepository _repository;
    private readonly MimirMapper _mapper;

    public GetUserByIdQueryHandler(
        IUserRepository repository,
        MimirMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        return _mapper.MapToUserDto(user);
    }
}
