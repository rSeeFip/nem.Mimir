using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Channels.Commands;

public sealed record UpdateChannelCommand(
    Guid ChannelId,
    string Name,
    string? Description) : ICommand;

public sealed class UpdateChannelCommandValidator : AbstractValidator<UpdateChannelCommand>
{
    public UpdateChannelCommandValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Channel name is required.")
            .MaximumLength(100).WithMessage("Channel name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage("Description must not exceed 500 characters.");
    }
}

public sealed class UpdateChannelHandler
{
    public async Task Handle(
        UpdateChannelCommand command,
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

        channel.Update(command.Name, command.Description);

        await repository.UpdateAsync(channel, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
