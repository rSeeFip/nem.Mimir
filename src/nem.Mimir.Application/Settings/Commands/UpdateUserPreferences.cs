using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Settings.Commands;

public sealed record UpdateUserPreferencesCommand(
    string? Section,
    Dictionary<string, object>? Values,
    Dictionary<string, Dictionary<string, object>>? Settings) : ICommand<UserPreferenceDto>;

public sealed class UpdateUserPreferencesCommandValidator : AbstractValidator<UpdateUserPreferencesCommand>
{
    public UpdateUserPreferencesCommandValidator()
    {
        RuleFor(x => x)
            .Must(HaveEitherSectionOrSettings)
            .WithMessage("Either section values or settings payload is required.");

        RuleFor(x => x.Section)
            .NotEmpty()
            .When(x => x.Values is not null)
            .WithMessage("Section is required when values are provided.");
    }

    private static bool HaveEitherSectionOrSettings(UpdateUserPreferencesCommand command)
    {
        var hasSectionPayload = !string.IsNullOrWhiteSpace(command.Section) && command.Values is not null;
        var hasFullPayload = command.Settings is not null;
        return hasSectionPayload || hasFullPayload;
    }
}

internal sealed class UpdateUserPreferencesCommandHandler
{
    private readonly IUserPreferenceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public UpdateUserPreferencesCommandHandler(
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

    public async Task<UserPreferenceDto> Handle(UpdateUserPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var preference = await _repository.GetByUserIdAsync(userId, cancellationToken)
            ?? await _repository.CreateAsync(Domain.Entities.UserPreference.Create(userId), cancellationToken);

        if (request.Settings is not null)
        {
            foreach (var (section, values) in request.Settings)
            {
                preference.UpdateSection(section, values);
            }
        }
        else
        {
            preference.UpdateSection(request.Section!, request.Values!);
        }

        await _repository.UpdateAsync(preference, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
