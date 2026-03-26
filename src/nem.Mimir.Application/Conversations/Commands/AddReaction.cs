using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Conversations.Commands;

public sealed record AddReactionCommand(
    Guid ConversationId,
    Guid MessageId,
    string Emoji) : ICommand<MessageDto>;

public sealed class AddReactionCommandValidator : AbstractValidator<AddReactionCommand>
{
    public AddReactionCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required.");

        RuleFor(x => x.Emoji)
            .NotEmpty().WithMessage("Emoji is required.")
            .MaximumLength(32).WithMessage("Emoji must not exceed 32 characters.");
    }
}

internal sealed class AddReactionCommandHandler
{
    public async Task<MessageDto> Handle(
        AddReactionCommand command,
        IConversationRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var conversation = await repository.GetWithMessagesAsync(command.ConversationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Conversation), command.ConversationId);

        if (conversation.UserId != userGuid)
            throw new ForbiddenAccessException();

        var message = conversation.Messages.FirstOrDefault(m => m.Id == command.MessageId)
            ?? throw new NotFoundException(nameof(Domain.Entities.Message), command.MessageId);

        message.AddReaction(command.Emoji, userGuid);

        await repository.UpdateAsync(conversation, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToMessageDto(message);
    }
}
