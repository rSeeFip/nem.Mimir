using FluentValidation;
using ChannelTypedId = nem.Contracts.Identity.ChannelId;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Channels.Queries;

public sealed record GetChannelMessagesQuery(
    Guid ChannelId,
    int PageNumber,
    int PageSize) : IQuery<PaginatedList<ChannelMessageDto>>;

public sealed class GetChannelMessagesQueryValidator : AbstractValidator<GetChannelMessagesQuery>
{
    public GetChannelMessagesQueryValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetChannelMessagesHandler
{
    public async Task<PaginatedList<ChannelMessageDto>> Handle(
        GetChannelMessagesQuery query,
        IChannelRepository repository,
        MimirMapper mapper,
        CancellationToken cancellationToken)
    {
        var result = await repository.GetMessagesAsync(
                ChannelTypedId.From(query.ChannelId),
                query.PageNumber,
                query.PageSize,
                cancellationToken)
            .ConfigureAwait(false);

        var items = result.Items.Select(mapper.MapToChannelMessageDto).ToList();
        return new PaginatedList<ChannelMessageDto>(items, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
