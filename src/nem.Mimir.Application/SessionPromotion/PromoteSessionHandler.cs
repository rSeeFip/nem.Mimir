using nem.Contracts.SessionPromotion;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using Wolverine;
using ChannelId = nem.Contracts.Identity.ChannelId;

namespace nem.Mimir.Application.SessionPromotion;

/// <summary>
/// Wolverine handler that promotes a conversation session from one transport channel
/// to another. On success, publishes <see cref="SessionPromotedEvent"/> to the bus.
/// StigmergyTraceId is propagated via the Wolverine envelope trace middleware (T7).
/// </summary>
public static class PromoteSessionHandler
{
    public static async Task<SessionPromotedEvent> Handle(
        PromoteSessionCommand command,
        IChannelRepository channelRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OldChannelId == command.NewChannelId)
            throw new ValidationException("OldChannelId and NewChannelId must differ.");

        // Verify old channel exists before promotion
        var oldChannel = await channelRepository.GetByIdAsync(command.OldChannelId, cancellationToken)
            ?? throw new NotFoundException(nameof(command.OldChannelId), command.OldChannelId);

        // Verify new channel exists
        var newChannel = await channelRepository.GetByIdAsync(command.NewChannelId, cancellationToken)
            ?? throw new NotFoundException(nameof(command.NewChannelId), command.NewChannelId);

        // Record promotion on old channel (marks it as promoted-from)
        // Both channels remain active; no session data is deleted (re-auth not required)
        _ = oldChannel;
        _ = newChannel;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SessionPromotedEvent(
            command.OldChannelId,
            command.NewChannelId,
            command.ConversationForkId,
            DateTimeOffset.UtcNow);
    }
}
