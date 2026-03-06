using MediatR;
using Microsoft.Extensions.Logging;

namespace Mimir.Application.ChannelEvents;

/// <summary>
/// Handles <see cref="IngestChannelEventCommand"/> by persisting the inbound channel event
/// and dispatching it for downstream agent processing.
/// </summary>
internal sealed class IngestChannelEventHandler(
    ILogger<IngestChannelEventHandler> logger) : IRequestHandler<IngestChannelEventCommand, ChannelEventResult>
{
    public Task<ChannelEventResult> Handle(IngestChannelEventCommand request, CancellationToken cancellationToken)
    {
        var eventId = Guid.NewGuid();

        logger.LogInformation(
            "Ingested channel event {EventId} from {Channel} user {ExternalUserId} in {ExternalChannelId}",
            eventId,
            request.Channel,
            request.ExternalUserId,
            request.ExternalChannelId);

        // TODO: Persist event as Message entity and dispatch to agent processing pipeline (T14+)
        // For now, accept the event and return a tracking ID.

        return Task.FromResult(new ChannelEventResult(eventId, Accepted: true));
    }
}
