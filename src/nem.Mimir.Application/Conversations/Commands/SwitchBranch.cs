using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Enums;

namespace nem.Mimir.Application.Conversations.Commands;

public sealed record SwitchBranchCommand(
    Guid ConversationId,
    Guid MessageId,
    int BranchIndex) : ICommand<MessageDto>;

public sealed class SwitchBranchCommandValidator : AbstractValidator<SwitchBranchCommand>
{
    public SwitchBranchCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required.");

        RuleFor(x => x.BranchIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Branch index must be zero or greater.");
    }
}

internal sealed class SwitchBranchCommandHandler
{
    public async Task<MessageDto> Handle(
        SwitchBranchCommand command,
        IConversationRepository repository,
        ICurrentUserService currentUser,
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

        if (message.Role != MessageRole.Assistant)
        {
            throw new nem.Mimir.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(command.MessageId)] = ["Branch switching is only supported for assistant messages."],
            });
        }

        var rootMessageId = message.ParentMessageId ?? message.Id;
        var branchMessage = conversation.Messages
            .Where(candidate =>
                candidate.Role == MessageRole.Assistant &&
                candidate.BranchIndex == command.BranchIndex &&
                (candidate.Id == rootMessageId || candidate.ParentMessageId == rootMessageId))
            .OrderBy(candidate => candidate.CreatedAt)
            .FirstOrDefault();

        if (branchMessage is null)
        {
            throw new NotFoundException("MessageBranch", $"{rootMessageId}:{command.BranchIndex}");
        }

        return mapper.MapToMessageDto(branchMessage);
    }
}
