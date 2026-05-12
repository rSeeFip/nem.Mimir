using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Channels.Queries;

public sealed record GetChannelByIdQuery(Guid ChannelId) : IQuery<ChannelDto>;

public sealed class GetChannelByIdQueryValidator : FluentValidation.AbstractValidator<GetChannelByIdQuery>
{
    public GetChannelByIdQueryValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");
    }
}

public sealed class GetChannelByIdHandler
{
    public async Task<ChannelDto> Handle(
        GetChannelByIdQuery query,
        IChannelRepository repository,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var channel = await repository.GetWithMembersAsync(ChannelTypedId.From(query.ChannelId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Channel), query.ChannelId);

        return mapper.MapToChannelDto(channel);
    }
}
