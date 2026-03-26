using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Settings.Queries;

public sealed record GetUserPreferencesBySectionQuery(string Section) : IQuery<UserPreferenceDto>;

public sealed class GetUserPreferencesBySectionQueryValidator : AbstractValidator<GetUserPreferencesBySectionQuery>
{
    public GetUserPreferencesBySectionQueryValidator()
    {
        RuleFor(x => x.Section)
            .NotEmpty().WithMessage("Section is required.");
    }
}

internal sealed class GetUserPreferencesBySectionQueryHandler
{
    private readonly IUserPreferenceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public GetUserPreferencesBySectionQueryHandler(
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

    public async Task<UserPreferenceDto> Handle(GetUserPreferencesBySectionQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var preference = await _repository.GetByUserIdAsync(userId, cancellationToken);

        if (preference is null)
        {
            preference = await _repository.CreateAsync(Domain.Entities.UserPreference.Create(userId), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var section = request.Section.Trim().ToLowerInvariant();

        if (!preference.Settings.TryGetValue(section, out var values))
            throw new NotFoundException("UserPreferenceSection", section);

        var dto = _mapper.MapToUserPreferenceDto(preference);

        var sectionSettings = new Dictionary<string, IReadOnlyDictionary<string, object>>(StringComparer.OrdinalIgnoreCase)
        {
            [section] = new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase),
        };

        return new UserPreferenceDto(
            dto.Id,
            dto.UserId,
            sectionSettings,
            dto.CreatedAt,
            dto.UpdatedAt);
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
