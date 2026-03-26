using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;

using ChannelDomainType = nem.Mimir.Domain.Enums.ChannelType;

namespace nem.Mimir.Application.Channels.Commands;

public sealed record CreateChannelCommand(
    string Name,
    string? Description,
    string Type) : ICommand<ChannelDto>;

public sealed class CreateChannelCommandValidator : AbstractValidator<CreateChannelCommand>
{
    public CreateChannelCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Channel name is required.")
            .MaximumLength(100).WithMessage("Channel name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Channel type is required.")
            .Must(value => Enum.TryParse<ChannelDomainType>(value, true, out _))
            .WithMessage("Channel type must be Public, Private, or DirectMessage.");
    }
}

public sealed class CreateChannelHandler
{
    public async Task<ChannelDto> Handle(
        CreateChannelCommand command,
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

        var channelType = Enum.Parse<ChannelDomainType>(command.Type, true);
        var channel = Channel.Create(userGuid, command.Name, command.Description, channelType);

        await repository.CreateAsync(channel, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return mapper.MapToChannelDto(channel);
    }
}
