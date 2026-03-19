namespace nem.Mimir.Infrastructure.Lifecycle;

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using nem.Contracts.Lifecycle;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Infrastructure.Persistence;

/// <summary>
/// EF Core read model pruning strategy for Mimir.
/// 
/// Mimir conversations are NOT rebuildable from events — there is no event source.
/// Therefore:
/// - Cold tier: Archive conversations (soft-delete via <c>ConversationStatus.Archived</c>)
/// - Frozen tier: Soft-delete only (hard delete is forbidden without prior export)
/// - HardDelete: NEVER returned by <see cref="GetPruneAction"/> since data is non-rebuildable
/// </summary>
public sealed class MimirReadModelPruningStrategy : IReadModelPruningStrategy
{
    private const string Service = "Mimir";
    private static readonly ActivitySource ActivitySource = new("nem.Mimir.Infrastructure.Lifecycle.MimirReadModelPruningStrategy");

    private readonly MimirDbContext _dbContext;
    private readonly ILogger _logger;

    public MimirReadModelPruningStrategy(
        MimirDbContext dbContext,
        ILoggerFactory loggerFactory)
    {
        _dbContext = dbContext;
        _logger = loggerFactory.CreateLogger<MimirReadModelPruningStrategy>();
    }

    /// <inheritdoc />
    public string ServiceId => Service;

    /// <inheritdoc />
    /// <remarks>
    /// Mimir is EF-only with no event source. Data cannot be rebuilt after deletion.
    /// </remarks>
    public bool IsRebuildableFromEvents => false;

    /// <inheritdoc />
    public bool ShouldPrune(object entity, RetentionPolicySnapshot policy, DateTimeOffset utcNow)
    {
        if (entity is not BaseAuditableEntity<Guid> auditable)
            return false;

        // Already soft-deleted — skip
        if (auditable.IsDeleted)
            return false;

        // Determine the entity's "last active" timestamp
        var lastActive = auditable.UpdatedAt ?? auditable.CreatedAt;
        var age = utcNow - lastActive;

        // Entity exceeds hot retention → eligible for pruning
        return age > policy.HotRetention;
    }

    /// <inheritdoc />
    public PruneAction GetPruneAction(StorageTier targetTier)
    {
        return targetTier switch
        {
            StorageTier.Cold => PruneAction.SoftDelete,
            StorageTier.Frozen => PruneAction.SoftDelete, // Never HardDelete — non-rebuildable
            _ => PruneAction.None,
        };
    }

    /// <inheritdoc />
    public async Task<int> ExecutePruningAsync(
        RetentionPolicySnapshot policy,
        StorageTier targetTier,
        PruneAction action,
        int maxEntities,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("pruning.execute", ActivityKind.Internal);
        activity?.SetTag("lifecycle.service_id", Service);
        activity?.SetTag("lifecycle.data_class", policy.DataClass);
        activity?.SetTag("lifecycle.target_tier", targetTier.ToString());
        activity?.SetTag("lifecycle.prune_action", action.ToString());
        activity?.SetTag("lifecycle.max_entities", maxEntities);

        // Safety guard: never hard-delete non-rebuildable data
        if (action == PruneAction.HardDelete)
        {
            _logger.LogWarning(
                "HardDelete requested for non-rebuildable Mimir data class {DataClass}. " +
                "Downgrading to SoftDelete for safety.",
                policy.DataClass);
            action = PruneAction.SoftDelete;
        }

        if (action is PruneAction.None)
            return 0;

        var utcNow = DateTimeOffset.UtcNow;
        var pruned = 0;

        pruned = policy.DataClass switch
        {
            "Conversations" => await PruneConversationsAsync(policy, utcNow, maxEntities, cancellationToken)
                .ConfigureAwait(false),
            "Messages" => await PruneMessagesAsync(policy, utcNow, maxEntities, cancellationToken)
                .ConfigureAwait(false),
            _ => 0,
        };

        if (pruned > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        activity?.SetTag("lifecycle.pruned_count", pruned);

        _logger.LogInformation(
            "Pruned {PrunedCount} {DataClass} entities in Mimir (tier: {TargetTier}, action: {Action})",
            pruned,
            policy.DataClass,
            targetTier,
            action);

        return pruned;
    }

    /// <summary>
    /// Archives active conversations that have exceeded their hot retention period.
    /// Uses the same <c>conversation.Archive()</c> pattern as <see cref="Services.ConversationArchiveService"/>.
    /// </summary>
    private async Task<int> PruneConversationsAsync(
        RetentionPolicySnapshot policy,
        DateTimeOffset utcNow,
        int maxEntities,
        CancellationToken cancellationToken)
    {
        var cutoff = utcNow - policy.HotRetention;

        var staleConversations = await _dbContext.Conversations
            .Where(c => c.Status == ConversationStatus.Active)
            .Where(c => (c.UpdatedAt ?? c.CreatedAt) < cutoff)
            .Take(maxEntities)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var conversation in staleConversations)
        {
            conversation.Archive();
        }

        return staleConversations.Count;
    }

    /// <summary>
    /// Counts messages in archived conversations that exceed retention.
    /// Messages inherit their lifecycle from the parent conversation — archiving the conversation
    /// is the canonical pruning action. Individual message deletion is not supported.
    /// </summary>
    private async Task<int> PruneMessagesAsync(
        RetentionPolicySnapshot policy,
        DateTimeOffset utcNow,
        int maxEntities,
        CancellationToken cancellationToken)
    {
        // Messages are pruned transitively via conversation archiving.
        // Direct message pruning returns 0 — the LifecyclePruningBackgroundService
        // should target "Conversations" data class for Mimir.
        _logger.LogDebug(
            "Message pruning in Mimir is handled transitively via conversation archiving. " +
            "Target 'Conversations' data class instead of 'Messages'.");

        return await Task.FromResult(0).ConfigureAwait(false);
    }
}
