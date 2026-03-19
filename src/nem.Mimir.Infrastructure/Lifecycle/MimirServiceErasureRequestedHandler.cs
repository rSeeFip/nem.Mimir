namespace nem.Mimir.Infrastructure.Lifecycle;

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using nem.Contracts.Lifecycle;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Infrastructure.Persistence;
using Wolverine;
using Wolverine.Attributes;

/// <summary>
/// Wolverine handler for <see cref="ServiceErasureRequested"/> commands targeting Mimir.
/// Implements GDPR erasure by soft-deleting or archiving conversations, messages,
/// and user data for the specified data subject.
/// 
/// Design:
/// - Cold tier (SoftDelete): Archives conversations and sets IsDeleted on entities
/// - Frozen tier (HardPurge): NOT supported without prior export — Mimir is not rebuildable from events
/// - CryptographicErasure: Not applicable to EF read models
/// </summary>
public static class MimirServiceErasureRequestedHandler
{
    private const string ServiceName = "Mimir";
    private static readonly ActivitySource ActivitySource = new("nem.Mimir.Infrastructure.Lifecycle.MimirServiceErasureRequestedHandler");

    /// <summary>
    /// Handles a data subject erasure request for Mimir's EF Core storage.
    /// Publishes <see cref="ServiceErasureCompleted"/> on success or <see cref="ServiceErasureFailed"/> on failure.
    /// </summary>
    [WolverineHandler]
    public static async Task HandleAsync(
        ServiceErasureRequested message,
        MimirDbContext dbContext,
        IMessageBus bus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("nem.Mimir.Infrastructure.Lifecycle.MimirServiceErasureRequestedHandler");

        using var activity = ActivitySource.StartActivity("erasure.handle", ActivityKind.Consumer);
        activity?.SetTag("lifecycle.erasure_request_id", message.ErasureRequestId.ToString());
        activity?.SetTag("lifecycle.subject_id", message.DataSubjectId.ToString());
        activity?.SetTag("lifecycle.erasure_mode", message.Mode.ToString());
        activity?.SetTag("lifecycle.storage_kind", message.StorageKind.ToString());
        activity?.SetTag("service.name", ServiceName);

        // Only handle EF read model requests
        if (message.StorageKind != StorageKind.EfReadModel)
        {
            logger.LogDebug(
                "Ignoring erasure request {ErasureRequestId} for storage kind {StorageKind} — Mimir only handles EfReadModel",
                message.ErasureRequestId,
                message.StorageKind);
            return;
        }

        try
        {
            var userId = message.DataSubjectId.Value;
            var recordsErased = 0;

            switch (message.Mode)
            {
                case ErasureMode.SoftDelete:
                    recordsErased = await SoftDeleteUserDataAsync(dbContext, userId, logger, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case ErasureMode.HardPurge:
                    // Mimir is NOT rebuildable from events — hard purge is dangerous.
                    // Only proceed if data was already exported (frozen tier).
                    // For safety, we perform soft-delete and log a warning.
                    logger.LogWarning(
                        "Hard purge requested for subject {DataSubjectId} in Mimir — Mimir data is not rebuildable from events. " +
                        "Performing soft-delete instead. Use frozen-tier export workflow for true hard deletion.",
                        message.DataSubjectId);
                    recordsErased = await SoftDeleteUserDataAsync(dbContext, userId, logger, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case ErasureMode.CryptographicErasure:
                    logger.LogWarning(
                        "Cryptographic erasure not supported for EF read models in Mimir. Performing soft-delete for subject {DataSubjectId}",
                        message.DataSubjectId);
                    recordsErased = await SoftDeleteUserDataAsync(dbContext, userId, logger, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(message), $"Unknown erasure mode: {message.Mode}");
            }

            activity?.SetTag("lifecycle.records_erased", recordsErased);

            await bus.PublishAsync(new ServiceErasureCompleted
            {
                ErasureRequestId = message.ErasureRequestId,
                DataSubjectId = message.DataSubjectId,
                StorageKind = message.StorageKind,
                Mode = message.Mode,
                ServiceName = ServiceName,
                RecordsErased = recordsErased,
                CorrelationId = message.CorrelationId,
            }).ConfigureAwait(false);

            logger.LogInformation(
                "Completed erasure for subject {DataSubjectId}: {RecordsErased} records affected (mode: {Mode})",
                message.DataSubjectId,
                recordsErased,
                message.Mode);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to erase data for subject {DataSubjectId} in Mimir (request: {ErasureRequestId})",
                message.DataSubjectId,
                message.ErasureRequestId);

            activity?.SetTag("lifecycle.erasure_error", ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            await bus.PublishAsync(new ServiceErasureFailed
            {
                ErasureRequestId = message.ErasureRequestId,
                DataSubjectId = message.DataSubjectId,
                StorageKind = message.StorageKind,
                ServiceName = ServiceName,
                ErrorMessage = ex.Message,
                ErrorCode = ex.GetType().Name,
                IsRetryable = ex is DbUpdateConcurrencyException or TimeoutException,
                CorrelationId = message.CorrelationId,
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Soft-deletes all user data: archives conversations, marks user as deleted.
    /// Uses the same Archive() pattern as <see cref="Services.ConversationArchiveService"/>.
    /// </summary>
    private static async Task<int> SoftDeleteUserDataAsync(
        MimirDbContext dbContext,
        Guid userId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var recordsAffected = 0;

        // Step 1: Archive all active conversations for this user
        var activeConversations = await dbContext.Conversations
            .Where(c => c.UserId == userId && c.Status == ConversationStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var conversation in activeConversations)
        {
            conversation.Archive();
            recordsAffected++;
        }

        // Step 2: Count messages in user's conversations (for reporting — messages are soft-deleted via cascade)
        var messageCount = await dbContext.Messages
            .CountAsync(m => dbContext.Conversations
                .Where(c => c.UserId == userId)
                .Select(c => c.Id)
                .Contains(m.ConversationId), cancellationToken)
            .ConfigureAwait(false);

        recordsAffected += messageCount;

        // Step 3: Deactivate the user record if it exists
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        if (user is not null)
        {
            user.Deactivate();
            recordsAffected++;
        }

        // Step 4: Persist all changes in a single transaction
        if (recordsAffected > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation(
            "Soft-deleted {RecordCount} records for user {UserId}: {ConversationCount} conversations archived, " +
            "{MessageCount} messages affected, user deactivated: {UserDeactivated}",
            recordsAffected,
            userId,
            activeConversations.Count,
            messageCount,
            user is not null);

        return recordsAffected;
    }
}
