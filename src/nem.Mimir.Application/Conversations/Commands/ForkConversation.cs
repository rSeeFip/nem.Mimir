using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Events;
using Wolverine;

namespace nem.Mimir.Application.Conversations.Commands;

public sealed record ForkConversationCommand(Guid ConversationId, string? ForkReason) : ICommand<ConversationDto>;

public sealed class ForkConversationCommandValidator : AbstractValidator<ForkConversationCommand>
{
    public ForkConversationCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.ForkReason)
            .NotEmpty().WithMessage("Fork reason is required.")
            .MaximumLength(1_000).WithMessage("Fork reason must not exceed 1000 characters.");
    }
}

internal sealed class ForkConversationCommandHandler(
    IConversationRepository repository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    MimirMapper mapper,
    IMessageBus messageBus)
{
    public async Task<ConversationDto> Handle(ForkConversationCommand command, CancellationToken ct)
    {
        var userId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
        {
            throw new ForbiddenAccessException("User identity could not be determined.");
        }

        var sourceConversation = await repository.GetByIdAsync(command.ConversationId, ct)
            ?? throw new NotFoundException(nameof(Conversation), command.ConversationId);

        if (sourceConversation.UserId != userGuid)
        {
            throw new ForbiddenAccessException();
        }

        var forkReason = command.ForkReason!;
        var forkedConversation = sourceConversation.Fork(userGuid, forkReason);

        await repository.CreateAsync(forkedConversation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var forkedEvent = forkedConversation.DomainEvents
            .OfType<ConversationForkedEvent>()
            .FirstOrDefault();

        if (forkedEvent is not null)
        {
            await messageBus.PublishAsync(forkedEvent);
        }

        return mapper.MapToConversationDto(forkedConversation);
    }
}
