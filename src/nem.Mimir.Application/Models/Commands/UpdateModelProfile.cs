using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Models.Commands;

public sealed record UpdateModelProfileCommand(
    ModelProfileId Id,
    string Name,
    string ModelId,
    decimal? Temperature,
    decimal? TopP,
    int? MaxTokens,
    decimal? FrequencyPenalty,
    decimal? PresencePenalty,
    IReadOnlyList<string>? StopSequences,
    string? SystemPromptOverride,
    string? ResponseFormat) : ICommand;

public sealed class UpdateModelProfileCommandValidator : AbstractValidator<UpdateModelProfileCommand>
{
    public UpdateModelProfileCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(ModelProfileId.Empty).WithMessage("Model profile ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Profile name is required.")
            .MaximumLength(200).WithMessage("Profile name must not exceed 200 characters.");

        RuleFor(x => x.ModelId)
            .NotEmpty().WithMessage("Model ID is required.")
            .MaximumLength(200).WithMessage("Model ID must not exceed 200 characters.");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0m, 2m)
            .When(x => x.Temperature.HasValue)
            .WithMessage("Temperature must be between 0 and 2.");

        RuleFor(x => x.TopP)
            .InclusiveBetween(0m, 1m)
            .When(x => x.TopP.HasValue)
            .WithMessage("Top P must be between 0 and 1.");

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0)
            .When(x => x.MaxTokens.HasValue)
            .WithMessage("Max tokens must be greater than 0.");

        RuleFor(x => x.FrequencyPenalty)
            .InclusiveBetween(-2m, 2m)
            .When(x => x.FrequencyPenalty.HasValue)
            .WithMessage("Frequency penalty must be between -2 and 2.");

        RuleFor(x => x.PresencePenalty)
            .InclusiveBetween(-2m, 2m)
            .When(x => x.PresencePenalty.HasValue)
            .WithMessage("Presence penalty must be between -2 and 2.");

        RuleForEach(x => x.StopSequences)
            .MaximumLength(200)
            .WithMessage("Each stop sequence must not exceed 200 characters.");

        RuleFor(x => x.SystemPromptOverride)
            .MaximumLength(10_000)
            .When(x => x.SystemPromptOverride is not null)
            .WithMessage("System prompt override must not exceed 10000 characters.");

        RuleFor(x => x.ResponseFormat)
            .MaximumLength(100)
            .When(x => x.ResponseFormat is not null)
            .WithMessage("Response format must not exceed 100 characters.");
    }
}

internal sealed class UpdateModelProfileCommandHandler
{
    private readonly IModelProfileRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateModelProfileCommandHandler(
        IModelProfileRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateModelProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var profile = await _repository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(ModelProfile), request.Id);

        if (profile.UserId != userId)
        {
            throw new ForbiddenAccessException();
        }

        var existingByName = await _repository
            .GetByNameForUserAsync(userId, request.Name, cancellationToken)
            .ConfigureAwait(false);

        if (existingByName is not null && existingByName.Id != request.Id)
        {
            throw new ConflictException($"A model profile named '{request.Name}' already exists.");
        }

        profile.Update(
            request.Name,
            request.ModelId,
            new ModelParameters(
                request.Temperature,
                request.TopP,
                request.MaxTokens,
                request.FrequencyPenalty,
                request.PresencePenalty,
                NormalizeStopSequences(request.StopSequences),
                string.IsNullOrWhiteSpace(request.SystemPromptOverride) ? null : request.SystemPromptOverride.Trim(),
                string.IsNullOrWhiteSpace(request.ResponseFormat) ? null : request.ResponseFormat.Trim()));

        await _repository.UpdateAsync(profile, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> NormalizeStopSequences(IReadOnlyList<string>? stopSequences) =>
        stopSequences is null
            ? Array.Empty<string>()
            : stopSequences
                .Where(static s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

    private static Guid ResolveCurrentUserId(ICurrentUserService currentUserService)
    {
        var userId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new ForbiddenAccessException("Current user identifier is invalid.");

        return parsedUserId;
    }
}
