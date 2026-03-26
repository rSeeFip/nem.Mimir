using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Models.Commands;

public sealed record SetArenaConfigCommand(
    IReadOnlyList<string> ModelIds,
    bool IsBlindComparisonEnabled,
    bool ShowModelNamesAfterVote) : ICommand<ArenaConfigDto>;

public sealed class SetArenaConfigCommandValidator : AbstractValidator<SetArenaConfigCommand>
{
    public SetArenaConfigCommandValidator()
    {
        RuleFor(x => x.ModelIds)
            .NotNull().WithMessage("Model IDs are required.")
            .Must(ids => ids.Count >= 2).WithMessage("Arena requires at least two models.");

        RuleForEach(x => x.ModelIds)
            .NotEmpty().WithMessage("Model ID cannot be empty.")
            .MaximumLength(200).WithMessage("Model ID must not exceed 200 characters.");
    }
}

internal sealed class SetArenaConfigCommandHandler
{
    private readonly IArenaConfigRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public SetArenaConfigCommandHandler(
        IArenaConfigRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ArenaConfigDto> Handle(SetArenaConfigCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var normalizedModelIds = request.ModelIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedModelIds.Count < 2)
        {
            throw new nem.Mimir.Application.Common.Exceptions.ValidationException("Arena requires at least two distinct model IDs.");
        }

        var config = await _repository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (config is null)
        {
            config = ArenaConfig.Create(
                userId,
                normalizedModelIds,
                request.IsBlindComparisonEnabled,
                request.ShowModelNamesAfterVote);

            await _repository.CreateAsync(config, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            config.Update(
                normalizedModelIds,
                request.IsBlindComparisonEnabled,
                request.ShowModelNamesAfterVote);

            await _repository.UpdateAsync(config, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return _mapper.MapToArenaConfigDto(config);
    }

    private static Guid ResolveCurrentUserId(ICurrentUserService currentUserService)
    {
        var userId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new ForbiddenAccessException("Current user identifier is invalid.");

        return parsedUserId;
    }
}
