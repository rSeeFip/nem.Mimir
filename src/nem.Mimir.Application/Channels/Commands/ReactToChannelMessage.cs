using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using ChannelMessageTypedId = nem.Contracts.Identity.ChannelMessageId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Channels.Commands;

public sealed record ReactToChannelMessageCommand(
    Guid ChannelId,
    Guid MessageId,
    string Emoji) : ICommand<ChannelMessageDto>;

public sealed class ReactToChannelMessageCommandValidator : AbstractValidator<ReactToChannelMessageCommand>
{
    public ReactToChannelMessageCommandValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required.");

        RuleFor(x => x.Emoji)
            .NotEmpty().WithMessage("Emoji is required.")
            .MaximumLength(32).WithMessage("Emoji must not exceed 32 characters.");
    }
}

public sealed class ReactToChannelMessageHandler
{
    public async Task<ChannelMessageDto> Handle(
        ReactToChannelMessageCommand command,
        IChannelRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var message = await repository.GetMessageByIdAsync(
                ChannelTypedId.From(command.ChannelId),
                ChannelMessageTypedId.From(command.MessageId),
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("ChannelMessage", command.MessageId);

        var alreadyReacted = message.Reactions.Any(r => r.UserId == userGuid && string.Equals(r.Emoji, command.Emoji, StringComparison.Ordinal));
        if (alreadyReacted)
        {
            message.RemoveReaction(userGuid, command.Emoji);
        }
        else
        {
            message.AddReaction(userGuid, command.Emoji);
        }

        await repository.UpdateMessageAsync(message, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToChannelMessageDto(message);
    }
}
