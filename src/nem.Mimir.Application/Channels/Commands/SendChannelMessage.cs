using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Channels.Commands;

public sealed record SendChannelMessageCommand(
    Guid ChannelId,
    string Content,
    Guid? ParentMessageId) : ICommand<ChannelMessageDto>;

public sealed class SendChannelMessageCommandValidator : AbstractValidator<SendChannelMessageCommand>
{
    public SendChannelMessageCommandValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(4000).WithMessage("Message content must not exceed 4000 characters.");
    }
}

public sealed class SendChannelMessageHandler
{
    public async Task<ChannelMessageDto> Handle(
        SendChannelMessageCommand command,
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

        var channel = await repository.GetWithMembersAsync(ChannelTypedId.From(command.ChannelId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Channel), command.ChannelId);

        var message = channel.AddMessage(userGuid, command.Content, command.ParentMessageId);

        await repository.UpdateAsync(channel, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToChannelMessageDto(message);
    }
}
