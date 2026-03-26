using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Channels.Commands;

public sealed record DeleteChannelCommand(Guid ChannelId) : ICommand;

public sealed class DeleteChannelCommandValidator : AbstractValidator<DeleteChannelCommand>
{
    public DeleteChannelCommandValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");
    }
}

public sealed class DeleteChannelHandler
{
    public async Task Handle(
        DeleteChannelCommand command,
        IChannelRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        var channel = await repository.GetByIdAsync(ChannelTypedId.From(command.ChannelId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Channel), command.ChannelId);

        if (channel.OwnerId != Guid.Parse(userId))
            throw new ForbiddenAccessException();

        await repository.DeleteAsync(channel.Id, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
