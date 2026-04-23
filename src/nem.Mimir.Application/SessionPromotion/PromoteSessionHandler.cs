using Marten;
using nem.Contracts.SessionPromotion;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.SessionPromotion;

public static class PromoteSessionHandler
{
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public static async Task<SessionPromotedEvent> Handle(
        PromoteSessionCommand command,
        IChannelRepository channelRepository,
        IUnitOfWork unitOfWork,
        IDocumentSession documentSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OldChannelId == command.NewChannelId)
            throw new ValidationException("OldChannelId and NewChannelId must differ.");

        if (command.IdempotencyKey.HasValue)
        {
            var tenantId = "default";
            var recordId = PromotionIdempotencyRecord.MakeId(tenantId, command.IdempotencyKey.Value);

            var existing = await documentSession.LoadAsync<PromotionIdempotencyRecord>(
                recordId, cancellationToken);

            if (existing is not null)
            {
                if (DateTimeOffset.UtcNow - existing.CreatedAt <= IdempotencyWindow)
                {
                    throw new PromotionIdempotencyConflictException(command.IdempotencyKey.Value, existing.CachedResult);
                }
            }

            var result = await ExecutePromotionAsync(command, channelRepository, unitOfWork, documentSession, cancellationToken);

            var record = new PromotionIdempotencyRecord
            {
                Id = recordId,
                IdempotencyKey = command.IdempotencyKey.Value,
                TenantId = tenantId,
                CachedResult = result,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            documentSession.Store(record);
            await documentSession.SaveChangesAsync(cancellationToken);

            return result;
        }

        return await ExecutePromotionAsync(command, channelRepository, unitOfWork, documentSession, cancellationToken);
    }

    private static async Task<SessionPromotedEvent> ExecutePromotionAsync(
        PromoteSessionCommand command,
        IChannelRepository channelRepository,
        IUnitOfWork unitOfWork,
        IDocumentSession documentSession,
        CancellationToken cancellationToken)
    {
        var oldChannel = await channelRepository.GetByIdAsync(command.OldChannelId, cancellationToken)
            ?? throw new NotFoundException(nameof(command.OldChannelId), command.OldChannelId);

        var newChannel = await channelRepository.GetByIdAsync(command.NewChannelId, cancellationToken)
            ?? throw new NotFoundException(nameof(command.NewChannelId), command.NewChannelId);

        _ = oldChannel;
        _ = newChannel;

        var promotedAt = DateTimeOffset.UtcNow;

        return new SessionPromotedEvent(
            command.OldChannelId,
            command.NewChannelId,
            command.ConversationForkId,
            promotedAt);
    }
}
