using Microsoft.Extensions.Logging;
using Mimir.Application.Knowledge;
using nem.Contracts.Memory;

namespace Mimir.Application.Services.Memory;

public sealed class MemoryConsolidationService : IMemoryConsolidator
{
    private static readonly string[] FlaggedTokens = ["flagged", "important", "critical", "action-item", "action_required"];

    private readonly IWorkingMemory _workingMemory;
    private readonly IEpisodicMemory _episodicMemory;
    private readonly ISemanticMemory _semanticMemory;
    private readonly IKnowHubBridge _knowHubBridge;
    private readonly MemoryConsolidationOptions _options;
    private readonly ILogger<MemoryConsolidationService> _logger;

    public MemoryConsolidationService(
        IWorkingMemory workingMemory,
        IEpisodicMemory episodicMemory,
        ISemanticMemory semanticMemory,
        IKnowHubBridge knowHubBridge,
        MemoryConsolidationOptions options,
        ILogger<MemoryConsolidationService> logger)
    {
        _workingMemory = workingMemory;
        _episodicMemory = episodicMemory;
        _semanticMemory = semanticMemory;
        _knowHubBridge = knowHubBridge;
        _options = options;
        _logger = logger;
    }

    public async Task ConsolidateAsync(
        string conversationId,
        MemoryConsolidationStrategy strategy = MemoryConsolidationStrategy.Balanced,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        string? userId = null;

        // Stage 1: Working -> Episodic
        try
        {
            userId = await TryResolveUserIdAsync(conversationId, cancellationToken).ConfigureAwait(false);
            await _episodicMemory.SummarizeSessionAsync(conversationId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Working->Episodic consolidation failed for conversation {ConversationId}.", conversationId);
        }

        // Stage 2: Episodic -> Semantic
        try
        {
            userId ??= await TryResolveUserIdAsync(conversationId, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning(
                    "Episodic->Semantic consolidation skipped for conversation {ConversationId}: unable to resolve user id.",
                    conversationId);
                return;
            }

            var episodes = await _episodicMemory
                .GetEpisodesAsync(userId, _options.MaxEpisodesPerConsolidation, cancellationToken)
                .ConfigureAwait(false);

            if (episodes.Count == 0)
            {
                return;
            }

            var episodesToConsolidate = SelectEpisodesForStrategy(episodes, strategy);
            if (episodesToConsolidate.Count == 0)
            {
                return;
            }

            if (!_knowHubBridge.IsAvailable)
            {
                _logger.LogWarning(
                    "KnowHub bridge unavailable. Skipping episodic->semantic distillation for conversation {ConversationId}.",
                    conversationId);
                return;
            }

            foreach (var episode in episodesToConsolidate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyList<DistilledKnowledge> distilled;
                try
                {
                    distilled = await _knowHubBridge
                        .DistillKnowledgeAsync(episode.Summary, $"episode:{episode.Id}", cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Distillation failed for episode {EpisodeId}.", episode.Id);
                    continue;
                }

                var factsToStore = strategy == MemoryConsolidationStrategy.Balanced
                    ? distilled.Where(x => x.CompositeScore > _options.EpisodicToSemanticThreshold)
                    : distilled;

                foreach (var item in factsToStore)
                {
                    if (string.IsNullOrWhiteSpace(item.Content) && string.IsNullOrWhiteSpace(item.Outcome))
                    {
                        continue;
                    }

                    var confidence = Math.Clamp((float)item.CompositeScore, 0f, 1f);
                    var fact = new KnowledgeFact(
                        Id: item.KnowledgeId?.ToString("N") ?? Guid.NewGuid().ToString("N"),
                        Subject: episode.UserId,
                        Predicate: string.IsNullOrWhiteSpace(item.Type) ? "learned" : item.Type,
                        Object: string.IsNullOrWhiteSpace(item.Content) ? item.Outcome : item.Content,
                        Confidence: confidence,
                        LearnedAt: episode.EndedAt,
                        Source: $"episode:{episode.Id}");

                    try
                    {
                        await _semanticMemory.StoreFactAsync(fact, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed storing semantic fact {FactId} from episode {EpisodeId}.", fact.Id, episode.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Episodic->Semantic consolidation failed for conversation {ConversationId}.", conversationId);
        }
    }

    private async Task<string?> TryResolveUserIdAsync(string conversationId, CancellationToken cancellationToken)
    {
        try
        {
            var recent = await _workingMemory.GetRecentAsync(conversationId, 20, cancellationToken).ConfigureAwait(false);
            if (recent.Count == 0)
            {
                return null;
            }

            foreach (var message in recent)
            {
                if (message.Metadata is null)
                {
                    continue;
                }

                if (message.Metadata.TryGetValue("userId", out var userId)
                    && !string.IsNullOrWhiteSpace(userId))
                {
                    return userId;
                }

                if (message.Metadata.TryGetValue("user_id", out var underscoredUserId)
                    && !string.IsNullOrWhiteSpace(underscoredUserId))
                {
                    return underscoredUserId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to derive user id from working memory for conversation {ConversationId}.", conversationId);
        }

        return null;
    }

    private static IReadOnlyList<Episode> SelectEpisodesForStrategy(
        IReadOnlyList<Episode> episodes,
        MemoryConsolidationStrategy strategy)
    {
        return strategy switch
        {
            MemoryConsolidationStrategy.Aggressive => episodes,
            MemoryConsolidationStrategy.Balanced => episodes,
            MemoryConsolidationStrategy.Conservative => episodes.Where(IsFlaggedEpisode).ToList(),
            _ => episodes
        };
    }

    private static bool IsFlaggedEpisode(Episode episode)
    {
        if (episode.KeyTopics.Any(topic => ContainsFlag(topic)))
        {
            return true;
        }

        return ContainsFlag(episode.Summary);
    }

    private static bool ContainsFlag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return FlaggedTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
