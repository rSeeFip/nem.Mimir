using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Enums;

namespace nem.Mimir.Application.Conversations.Commands;

public sealed record EditMessageCommand(
    Guid ConversationId,
    Guid MessageId,
    string Content) : ICommand<MessageDto>;

public sealed class EditMessageCommandValidator : AbstractValidator<EditMessageCommand>
{
    public EditMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(32000).WithMessage("Message content must not exceed 32000 characters.");
    }
}

internal sealed class EditMessageCommandHandler
{
    public async Task<MessageDto> Handle(
        EditMessageCommand command,
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

        if (message.Role != MessageRole.User)
            throw new nem.Mimir.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(command.MessageId)] = ["Only user messages can be edited."],
            });

        message.Edit(command.Content);

        await repository.UpdateAsync(conversation, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToMessageDto(message);
    }
}
