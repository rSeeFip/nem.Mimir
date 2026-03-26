using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Channels.Commands;

public sealed record JoinChannelCommand(Guid ChannelId) : ICommand;

public sealed class JoinChannelCommandValidator : AbstractValidator<JoinChannelCommand>
{
    public JoinChannelCommandValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");
    }
}

public sealed class JoinChannelHandler
{
    public async Task Handle(
        JoinChannelCommand command,
        IChannelRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        var channel = await repository.GetWithMembersAsync(ChannelTypedId.From(command.ChannelId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Channel), command.ChannelId);

        channel.AddMember(Guid.Parse(userId));

        await repository.UpdateAsync(channel, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
