using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Channels.Commands;

public sealed record LeaveChannelCommand(Guid ChannelId) : ICommand;

public sealed class LeaveChannelCommandValidator : AbstractValidator<LeaveChannelCommand>
{
    public LeaveChannelCommandValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");
    }
}

public sealed class LeaveChannelHandler
{
    public async Task Handle(
        LeaveChannelCommand command,
        IChannelRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var channel = await repository.GetWithMembersAsync(ChannelTypedId.From(command.ChannelId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Channel), command.ChannelId);

        channel.RemoveMember(userGuid);

        await repository.UpdateAsync(channel, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
