using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Evaluations.Commands;

public sealed record StartArenaSessionCommand(string Prompt) : ICommand<ArenaSessionDto>;

public sealed record ArenaSessionDto(
    Guid SessionId,
    string Prompt,
    string ResponseA,
    string ResponseB,
    bool IsRevealed,
    string? ModelAName,
    string? ModelBName,
    string? VotedWinner,
    decimal? ModelAEloBefore,
    decimal? ModelAEloAfter,
    decimal? ModelBEloBefore,
    decimal? ModelBEloAfter);

public sealed class StartArenaSessionCommandValidator : AbstractValidator<StartArenaSessionCommand>
{
    public StartArenaSessionCommandValidator()
    {
        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("Prompt is required.")
            .MaximumLength(4000).WithMessage("Prompt must not exceed 4000 characters.");
    }
}

public sealed class StartArenaSessionHandler
{
    public async Task<ArenaSessionDto> Handle(
        StartArenaSessionCommand command,
        IArenaConfigRepository arenaConfigRepository,
        IModelProfileRepository modelProfileRepository,
        IEvaluationRepository evaluationRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var arenaConfig = await arenaConfigRepository.GetByUserIdAsync(userGuid, cancellationToken).ConfigureAwait(false);
        if (arenaConfig is null || arenaConfig.ModelIds.Count < 2)
        {
            throw new nem.Mimir.Application.Common.Exceptions.ValidationException(
                new Dictionary<string, string[]>
                {
                    [nameof(command.Prompt)] = ["Arena requires at least two configured models."],
                });
        }

        var profiles = await modelProfileRepository.GetByUserIdAsync(userGuid, cancellationToken).ConfigureAwait(false);
        var profileByModelId = profiles
            .GroupBy(profile => profile.ModelId)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var selectedIds = arenaConfig.ModelIds
            .Where(modelId => !string.IsNullOrWhiteSpace(modelId) && profileByModelId.ContainsKey(modelId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();

        if (selectedIds.Length < 2)
        {
            throw new nem.Mimir.Application.Common.Exceptions.ValidationException(
                new Dictionary<string, string[]>
                {
                    [nameof(command.Prompt)] = ["Configured arena models could not be resolved for this user."],
                });
        }

        var modelA = profileByModelId[selectedIds[0]];
        var modelB = profileByModelId[selectedIds[1]];

        var responseA = $"Candidate A response for prompt: {command.Prompt.Trim()}";
        var responseB = $"Candidate B response for prompt: {command.Prompt.Trim()}";

        var session = ArenaSession.Create(
            userGuid,
            command.Prompt,
            modelA.Id.Value,
            modelB.Id.Value,
            responseA,
            responseB);

        await evaluationRepository.CreateArenaSessionAsync(session, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ArenaSessionDto(
            session.Id,
            session.Prompt,
            session.ResponseA,
            session.ResponseB,
            session.IsRevealed,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
