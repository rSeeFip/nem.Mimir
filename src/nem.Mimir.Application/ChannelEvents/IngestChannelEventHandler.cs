using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.ChannelEvents;

internal sealed class IngestChannelEventHandler(
    IChannelEventRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<IngestChannelEventHandler> logger)
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
