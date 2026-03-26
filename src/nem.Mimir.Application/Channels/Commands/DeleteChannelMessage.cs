using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using ChannelMessageTypedId = nem.Contracts.Identity.ChannelMessageId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Channels.Commands;

public sealed record DeleteChannelMessageCommand(Guid ChannelId, Guid MessageId) : ICommand;

public sealed class DeleteChannelMessageCommandValidator : AbstractValidator<DeleteChannelMessageCommand>
{
    public DeleteChannelMessageCommandValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required.");
    }
}

public sealed class DeleteChannelMessageHandler
{
    public async Task Handle(
        DeleteChannelMessageCommand command,
        IChannelRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
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

        if (message.SenderId != userGuid)
            throw new ForbiddenAccessException();

        await repository.DeleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
