using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Evaluations.Commands;

public sealed record CreateEvaluationCommand(
    Guid ModelAId,
    Guid ModelBId,
    string Prompt,
    string ResponseA,
    string ResponseB) : ICommand<EvaluationDto>;

public sealed class CreateEvaluationCommandValidator : AbstractValidator<CreateEvaluationCommand>
{
    public CreateEvaluationCommandValidator()
    {
        RuleFor(x => x.ModelAId)
            .NotEmpty().WithMessage("Model A is required.");

        RuleFor(x => x.ModelBId)
            .NotEmpty().WithMessage("Model B is required.");

        RuleFor(x => x)
            .Must(x => x.ModelAId != x.ModelBId)
            .WithMessage("Model A and Model B must be different.");

        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("Prompt is required.")
            .MaximumLength(8000).WithMessage("Prompt must not exceed 8000 characters.");

        RuleFor(x => x.ResponseA)
            .NotEmpty().WithMessage("Response A is required.");

        RuleFor(x => x.ResponseB)
            .NotEmpty().WithMessage("Response B is required.");
    }
}

public sealed class CreateEvaluationHandler
{
    public async Task<EvaluationDto> Handle(
        CreateEvaluationCommand command,
        IEvaluationRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var evaluation = Evaluation.Create(
            userGuid,
            command.ModelAId,
            command.ModelBId,
            command.Prompt,
            command.ResponseA,
            command.ResponseB);

        await repository.CreateEvaluationAsync(evaluation, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToEvaluationDto(evaluation);
    }
}
