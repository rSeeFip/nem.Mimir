using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using ChannelMessageTypedId = nem.Contracts.Identity.ChannelMessageId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Channels.Commands;

public sealed record EditChannelMessageCommand(
    Guid ChannelId,
    Guid MessageId,
    string Content) : ICommand<ChannelMessageDto>;

public sealed class EditChannelMessageCommandValidator : AbstractValidator<EditChannelMessageCommand>
{
    public EditChannelMessageCommandValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(4000).WithMessage("Message content must not exceed 4000 characters.");
    }
}

public sealed class EditChannelMessageHandler
{
    public async Task<ChannelMessageDto> Handle(
        EditChannelMessageCommand command,
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

        if (message.SenderId != userGuid)
            throw new ForbiddenAccessException();

        message.Edit(command.Content);

        await repository.UpdateMessageAsync(message, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToChannelMessageDto(message);
    }
}
