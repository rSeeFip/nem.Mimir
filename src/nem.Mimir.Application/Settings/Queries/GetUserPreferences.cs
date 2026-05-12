using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Settings.Queries;

public sealed record GetUserPreferencesQuery : IQuery<UserPreferenceDto>;

public sealed class GetUserPreferencesQueryValidator : AbstractValidator<GetUserPreferencesQuery>
{
}

internal sealed class GetUserPreferencesQueryHandler
{
    private readonly IUserPreferenceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public GetUserPreferencesQueryHandler(
        IUserPreferenceRepository repository,
        IUnitOfWork unitOfWork,
        MimirMapper mapper,
        ICurrentUserService currentUserService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUserService = currentUserService;
    }

    public async Task<UserPreferenceDto> Handle(GetUserPreferencesQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var preference = await _repository.GetByUserIdAsync(userId, cancellationToken);

        if (preference is null)
        {
            preference = await _repository.CreateAsync(Domain.Entities.UserPreference.Create(userId), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return _mapper.MapToUserPreferenceDto(preference);
    }

    private static Guid ResolveCurrentUserId(ICurrentUserService currentUserService)
    {
        var currentUserId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(currentUserId, out var userId))
            throw new ForbiddenAccessException("Current user identifier is invalid.");

        return userId;
    }
}
