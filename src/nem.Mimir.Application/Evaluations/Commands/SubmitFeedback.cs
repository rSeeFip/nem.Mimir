using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using EvaluationId = nem.Mimir.Domain.ValueObjects.EvaluationId;

namespace nem.Mimir.Application.Evaluations.Commands;

public sealed record SubmitFeedbackCommand(
    Guid EvaluationId,
    int Quality,
    int Relevance,
    int Accuracy,
    string? Comment) : ICommand<EvaluationFeedbackDto>;

public sealed class SubmitFeedbackCommandValidator : AbstractValidator<SubmitFeedbackCommand>
{
    public SubmitFeedbackCommandValidator()
    {
        RuleFor(x => x.EvaluationId)
            .NotEmpty().WithMessage("Evaluation ID is required.");

        RuleFor(x => x.Quality)
            .InclusiveBetween(1, 5).WithMessage("Quality must be between 1 and 5.");

        RuleFor(x => x.Relevance)
            .InclusiveBetween(1, 5).WithMessage("Relevance must be between 1 and 5.");

        RuleFor(x => x.Accuracy)
            .InclusiveBetween(1, 5).WithMessage("Accuracy must be between 1 and 5.");

        RuleFor(x => x.Comment)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.Comment))
            .WithMessage("Comment must not exceed 2000 characters.");
    }
}

public sealed class SubmitFeedbackHandler
{
    public async Task<EvaluationFeedbackDto> Handle(
        SubmitFeedbackCommand command,
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

        var evaluation = await repository.GetByIdAsync(EvaluationId.From(command.EvaluationId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Evaluation), command.EvaluationId);

        if (evaluation.Status != Domain.Enums.EvaluationStatus.Completed)
            throw new nem.Mimir.Application.Common.Exceptions.ValidationException(
                new Dictionary<string, string[]>
                {
                    [nameof(command.EvaluationId)] = ["Feedback can be submitted only for completed evaluations."],
                });

        var feedback = EvaluationFeedback.Create(
            evaluation.Id,
            userGuid,
            command.Quality,
            command.Relevance,
            command.Accuracy,
            command.Comment);

        await repository.CreateFeedbackAsync(feedback, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToEvaluationFeedbackDto(feedback);
    }
}
