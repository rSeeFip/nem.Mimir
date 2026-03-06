using MediatR;
using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Entities;

namespace Mimir.Application.ChannelEvents;

internal sealed class IngestChannelEventHandler(
    IChannelEventRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<IngestChannelEventHandler> logger) : IRequestHandler<IngestChannelEventCommand, ChannelEventResult>
{
    public async Task<ChannelEventResult> Handle(IngestChannelEventCommand request, CancellationToken cancellationToken)
    {
        var channelEvent = ChannelEvent.Create(
            request.Channel,
            request.ExternalChannelId,
            request.ExternalUserId,
            request.Content,
            request.Timestamp);

        await repository.CreateAsync(channelEvent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Ingested channel event {EventId} from {Channel} user {ExternalUserId} in {ExternalChannelId}",
            channelEvent.Id,
            request.Channel,
            request.ExternalUserId,
            request.ExternalChannelId);

        return new ChannelEventResult(channelEvent.Id, Accepted: true);
    }
}
