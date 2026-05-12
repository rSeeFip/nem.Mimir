using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Channels.Queries;

public sealed record GetChannelMembersQuery(Guid ChannelId) : IQuery<IReadOnlyList<ChannelMemberDto>>;

public sealed class GetChannelMembersQueryValidator : AbstractValidator<GetChannelMembersQuery>
{
    public GetChannelMembersQueryValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");
    }
}

public sealed class GetChannelMembersHandler
{
    public async Task<IReadOnlyList<ChannelMemberDto>> Handle(
        GetChannelMembersQuery query,
        IChannelRepository repository,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var channel = await repository.GetWithMembersAsync(ChannelTypedId.From(query.ChannelId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Channel), query.ChannelId);

        return channel.Members.Select(mapper.MapToChannelMemberDto).ToList();
    }
}
