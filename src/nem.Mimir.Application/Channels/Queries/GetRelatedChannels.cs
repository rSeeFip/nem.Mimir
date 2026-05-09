using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Channels.Queries;

public sealed record GetRelatedChannelsQuery(Guid ConversationId) : IQuery<IReadOnlyList<ChannelListDto>>;

public sealed class GetRelatedChannelsQueryValidator : AbstractValidator<GetRelatedChannelsQuery>
{
    public GetRelatedChannelsQueryValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");
    }
}

public sealed class GetRelatedChannelsHandler
{
    public static async Task<IReadOnlyList<ChannelListDto>> Handle(
        GetRelatedChannelsQuery query,
        IChannelRepository repository,
        ICurrentUserService currentUser,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var channels = await repository
            .GetBySourceConversationIdAsync(query.ConversationId, userGuid, cancellationToken)
            .ConfigureAwait(false);

        return channels.Select(mapper.MapToChannelListDto).ToList();
    }
}
