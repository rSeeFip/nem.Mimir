namespace nem.Mimir.Infrastructure.Lifecycle;

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using nem.Contracts.Lifecycle;
using nem.Contracts.Lifecycle.DataSubject;
using nem.Mimir.Infrastructure.Persistence;

/// <summary>
/// Contributes Mimir's data subject registrations to the lifecycle management system.
/// Locates conversations, messages, and user records tied to a data subject (UserId).
/// </summary>
public sealed class MimirDataSubjectContributor : IDataSubjectContributor
{
    private const string Service = "Mimir";
    private static readonly ActivitySource ActivitySource = new("nem.Mimir.Infrastructure.Lifecycle.MimirDataSubjectContributor");

    private readonly MimirDbContext _dbContext;
    private readonly ILogger _logger;

    public MimirDataSubjectContributor(
        MimirDbContext dbContext,
        ILoggerFactory loggerFactory)
    {
        _dbContext = dbContext;
        _logger = loggerFactory.CreateLogger<MimirDataSubjectContributor>();
    }

    /// <inheritdoc />
    public string ContributorName => Service;

    /// <inheritdoc />
    public async Task LinkDataSubjectAsync(DataSubjectId dataSubjectId, RetentionPolicyId policyId, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("data_subject.link", ActivityKind.Internal);
        activity?.SetTag("lifecycle.subject_id", dataSubjectId.ToString());
        activity?.SetTag("lifecycle.policy_id", policyId.ToString());
        activity?.SetTag("service.name", Service);

        _logger.LogInformation(
            "Linking data subject {DataSubjectId} to retention policy {PolicyId} in Mimir",
            dataSubjectId,
            policyId);

        // Mimir links subjects implicitly via UserId on Conversations.
        // No explicit link table needed — FindLocatorsAsync discovers data dynamically.
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UnlinkDataSubjectAsync(DataSubjectId dataSubjectId, RetentionPolicyId policyId, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("data_subject.unlink", ActivityKind.Internal);
        activity?.SetTag("lifecycle.subject_id", dataSubjectId.ToString());
        activity?.SetTag("lifecycle.policy_id", policyId.ToString());
        activity?.SetTag("service.name", Service);

        _logger.LogInformation(
            "Unlinking data subject {DataSubjectId} from retention policy {PolicyId} in Mimir",
            dataSubjectId,
            policyId);

        // Implicit linking — no explicit state to remove.
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> HasDataSubjectAsync(DataSubjectId dataSubjectId, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("data_subject.has", ActivityKind.Internal);
        activity?.SetTag("lifecycle.subject_id", dataSubjectId.ToString());
        activity?.SetTag("service.name", Service);

        var userId = dataSubjectId.Value;

        var hasUser = await _dbContext.Users
            .AnyAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        var hasConversations = !hasUser && await _dbContext.Conversations
            .AnyAsync(c => c.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        var exists = hasUser || hasConversations;

        activity?.SetTag("lifecycle.subject_exists", exists);

        return exists;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DataSubjectLocator>> FindLocatorsAsync(DataSubjectId subjectId, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("data_subject.find_locators", ActivityKind.Internal);
        activity?.SetTag("lifecycle.subject_id", subjectId.ToString());
        activity?.SetTag("service.name", Service);

        var userId = subjectId.Value;
        var now = DateTimeOffset.UtcNow;
        var locators = new List<DataSubjectLocator>();

        // Locator for User record
        var hasUser = await _dbContext.Users
            .AnyAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        if (hasUser)
        {
            locators.Add(new DataSubjectLocator
            {
                ServiceId = Service,
                DataClass = "Users",
                Kind = StorageKind.EfReadModel,
                LocatorToken = userId.ToString("N"),
                CurrentTier = StorageTier.Hot,
                LastSeenAt = now,
            });
        }

        // Locator for Conversations
        var conversationCount = await _dbContext.Conversations
            .CountAsync(c => c.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (conversationCount > 0)
        {
            locators.Add(new DataSubjectLocator
            {
                ServiceId = Service,
                DataClass = "Conversations",
                Kind = StorageKind.EfReadModel,
                LocatorToken = userId.ToString("N"),
                CurrentTier = StorageTier.Hot,
                LastSeenAt = now,
            });
        }

        // Locator for Messages (child data via conversations)
        var hasMessages = conversationCount > 0 && await _dbContext.Messages
            .AnyAsync(m => _dbContext.Conversations
                .Where(c => c.UserId == userId)
                .Select(c => c.Id)
                .Contains(m.ConversationId), cancellationToken)
            .ConfigureAwait(false);

        if (hasMessages)
        {
            locators.Add(new DataSubjectLocator
            {
                ServiceId = Service,
                DataClass = "Messages",
                Kind = StorageKind.EfReadModel,
                LocatorToken = userId.ToString("N"),
                CurrentTier = StorageTier.Hot,
                LastSeenAt = now,
            });
        }

        activity?.SetTag("lifecycle.locator_count", locators.Count);

        _logger.LogInformation(
            "Found {LocatorCount} data locators for subject {DataSubjectId} in Mimir",
            locators.Count,
            subjectId);

        return locators;
    }

    /// <inheritdoc />
    public async Task RegisterSubjectAsync(DataSubjectId subjectId, DataSubjectLocator locator, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("data_subject.register", ActivityKind.Internal);
        activity?.SetTag("lifecycle.subject_id", subjectId.ToString());
        activity?.SetTag("lifecycle.data_class", locator.DataClass);
        activity?.SetTag("service.name", Service);

        _logger.LogInformation(
            "Registering data subject {DataSubjectId} locator for {DataClass} in Mimir",
            subjectId,
            locator.DataClass);

        // Mimir uses implicit registration via UserId foreign keys.
        // No explicit registry needed — FindLocatorsAsync discovers data dynamically.
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
