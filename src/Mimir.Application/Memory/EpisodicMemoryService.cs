using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Knowledge;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using nem.Contracts.Memory;

namespace Mimir.Application.Services.Memory;

public sealed class EpisodicMemoryService : IEpisodicMemory
{
    private const string EpisodicMarker = "[episodic-memory]";
    private const int ConversationPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Lock CacheSync = new();
    private static readonly Dictionary<Guid, EpisodicMemoryDocument> CacheByDocumentId = [];

    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKnowHubBridge _knowHubBridge;
    private readonly EpisodicMemoryOptions _options;
    private readonly ILogger<EpisodicMemoryService> _logger;

    internal static void ResetCacheForTesting()
    {
        lock (CacheSync)
        {
            CacheByDocumentId.Clear();
        }
    }

    public EpisodicMemoryService(
        IConversationRepository conversationRepository,
        IUnitOfWork unitOfWork,
        IKnowHubBridge knowHubBridge,
        EpisodicMemoryOptions options,
        ILogger<EpisodicMemoryService> logger)
    {
        _conversationRepository = conversationRepository;
        _unitOfWork = unitOfWork;
        _knowHubBridge = knowHubBridge;
        _options = options;
        _logger = logger;
    }

    public async Task SaveEpisodeAsync(Episode episode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(episode);

        if (!Guid.TryParse(episode.ConversationId, out var conversationGuid))
        {
            throw new ArgumentException("Conversation ID must be a valid GUID.", nameof(episode));
        }

        var conversation = await _conversationRepository
            .GetWithMessagesAsync(conversationGuid, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            throw new InvalidOperationException($"Conversation '{episode.ConversationId}' not found.");
        }

        float[]? embedding = null;

        if (_knowHubBridge.IsAvailable)
        {
            try
            {
                embedding = await _knowHubBridge
                    .GenerateEmbeddingAsync(episode.Summary, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding generation failed for episode {EpisodeId}. Persisting without embedding.", episode.Id);
            }
        }

        if (embedding is not null && embedding.Length != _options.EmbeddingDimension)
        {
            _logger.LogWarning(
                "Embedding dimension mismatch for episode {EpisodeId}. Expected {ExpectedDimension}, got {ActualDimension}. Persisting without embedding.",
                episode.Id,
                _options.EmbeddingDimension,
                embedding.Length);
            embedding = null;
        }

        var document = EpisodicMemoryDocument.FromEpisode(episode, embedding);
        var serialized = JsonSerializer.Serialize(document, JsonOptions);

        var message = conversation.AddMessage(MessageRole.System, $"{EpisodicMarker}{serialized}");
        message.SetTokenCount(EstimateTokenCount(serialized));

        await _conversationRepository.UpdateAsync(conversation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        lock (CacheSync)
        {
            CacheByDocumentId[document.Id] = document;
            EnforceUserLimit(episode.UserId);
        }
    }

    public async Task<IReadOnlyList<Episode>> GetEpisodesAsync(string userId, int count = 10, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (count <= 0)
        {
            return Array.Empty<Episode>();
        }

        if (!Guid.TryParse(userId, out var userGuid))
        {
            return Array.Empty<Episode>();
        }

        var documents = await LoadUserDocumentsAsync(userGuid, cancellationToken).ConfigureAwait(false);

        lock (CacheSync)
        {
            foreach (var doc in documents)
            {
                CacheByDocumentId[doc.Id] = doc;
            }
        }

        return documents
            .OrderByDescending(x => x.EndedAt)
            .Take(count)
            .Select(x => x.ToEpisode())
            .ToList();
    }

    public async Task<IReadOnlyList<Episode>> SearchSimilarAsync(string query, int count = 5, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        if (count <= 0 || !_knowHubBridge.IsAvailable)
        {
            return Array.Empty<Episode>();
        }

        float[]? queryEmbedding;
        try
        {
            queryEmbedding = await _knowHubBridge
                .GenerateEmbeddingAsync(query, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding generation failed for episodic similarity query.");
            return Array.Empty<Episode>();
        }

        List<EpisodeSearchHit> localHits;
        lock (CacheSync)
        {
            localHits = CacheByDocumentId.Values
                .Where(x => x.EmbeddingVector is { Length: > 0 })
                .Select(x => new EpisodeSearchHit(x, CosineSimilarity(queryEmbedding, x.EmbeddingVector!)))
                .Where(x => x.Score >= _options.MinQualityScore)
                .OrderByDescending(x => x.Score)
                .Take(count)
                .ToList();
        }

        IReadOnlyList<KnowledgeSearchResult> bridgeResults;
        try
        {
            bridgeResults = await _knowHubBridge
                .SearchKnowledgeAsync(query, count * 2, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KnowHub fallback search failed for episodic memory query.");
            return localHits.Select(x => x.Document.ToEpisode()).ToList();
        }

        if (bridgeResults.Count == 0)
        {
            return localHits.Select(x => x.Document.ToEpisode()).ToList();
        }

        var hintedConversationIds = bridgeResults
            .SelectMany(result => EnumeratePossibleConversationIds(result))
            .Distinct()
            .ToList();

        var hintedDocuments = await LoadDocumentsByConversationIdsAsync(hintedConversationIds, cancellationToken).ConfigureAwait(false);

        lock (CacheSync)
        {
            foreach (var document in hintedDocuments)
            {
                CacheByDocumentId[document.Id] = document;
            }
        }

        var merged = localHits
            .Concat(hintedDocuments.Select(x => new EpisodeSearchHit(x, SimilarityForDocument(x, queryEmbedding))))
            .Where(x => x.Score >= _options.MinQualityScore)
            .GroupBy(x => x.Document.Id)
            .Select(x => x.OrderByDescending(hit => hit.Score).First())
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select(x => x.Document.ToEpisode())
            .ToList();

        return merged;
    }

    public async Task<string> SummarizeSessionAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        if (!Guid.TryParse(conversationId, out var conversationGuid))
        {
            throw new ArgumentException("Conversation ID must be a valid GUID.", nameof(conversationId));
        }

        var conversation = await _conversationRepository
            .GetWithMessagesAsync(conversationGuid, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            throw new InvalidOperationException($"Conversation '{conversationId}' not found.");
        }

        var messages = conversation.Messages
            .Where(x => x.Role != MessageRole.System || !x.Content.StartsWith(EpisodicMarker, StringComparison.Ordinal))
            .OrderBy(x => x.CreatedAt)
            .ToList();

        if (messages.Count == 0)
        {
            return "No conversation messages to summarize.";
        }

        var content = string.Join(Environment.NewLine, messages.Select(m => $"[{m.Role}] {m.Content}"));
        var distilledKnowledge = Array.Empty<DistilledKnowledge>();

        if (_knowHubBridge.IsAvailable)
        {
            try
            {
                distilledKnowledge = (await _knowHubBridge
                    .DistillKnowledgeAsync(content, $"conversation:{conversation.Id}", cancellationToken)
                    .ConfigureAwait(false))
                    .Where(x => x.CompositeScore >= _options.MinQualityScore)
                    .ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Distillation failed for conversation {ConversationId}.", conversationId);
            }
        }

        var summary = distilledKnowledge.Length > 0
            ? string.Join(
                " ",
                distilledKnowledge
                    .Select(x => x.Outcome)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal))
            : BuildFallbackSummary(conversation, messages);

        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = BuildFallbackSummary(conversation, messages);
        }

        var keyTopics = distilledKnowledge
            .Select(x => x.Type)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (keyTopics.Count == 0)
        {
            keyTopics = ExtractFallbackTopics(messages);
        }

        var episode = new Episode(
            Id: Guid.NewGuid().ToString("N"),
            UserId: conversation.UserId.ToString(),
            ConversationId: conversation.Id.ToString(),
            Summary: summary,
            KeyTopics: keyTopics,
            StartedAt: messages[0].CreatedAt,
            EndedAt: messages[^1].CreatedAt,
            MessageCount: messages.Count);

        await SaveEpisodeAsync(episode, cancellationToken).ConfigureAwait(false);
        return summary;
    }

    private async Task<List<EpisodicMemoryDocument>> LoadUserDocumentsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var page = 1;
        var loaded = new List<EpisodicMemoryDocument>();

        while (true)
        {
            var conversationsPage = await _conversationRepository
                .GetByUserIdAsync(userId, page, ConversationPageSize, cancellationToken)
                .ConfigureAwait(false);

            if (conversationsPage.Items.Count == 0)
            {
                break;
            }

            foreach (var conversation in conversationsPage.Items)
            {
                var full = await _conversationRepository
                    .GetWithMessagesAsync(conversation.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (full is null)
                {
                    continue;
                }

                loaded.AddRange(ExtractDocuments(full));
            }

            if (page >= conversationsPage.TotalPages)
            {
                break;
            }

            page++;
        }

        return loaded;
    }

    private async Task<List<EpisodicMemoryDocument>> LoadDocumentsByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        var loaded = new List<EpisodicMemoryDocument>();

        foreach (var conversationId in conversationIds)
        {
            var conversation = await _conversationRepository
                .GetWithMessagesAsync(conversationId, cancellationToken)
                .ConfigureAwait(false);

            if (conversation is null)
            {
                continue;
            }

            loaded.AddRange(ExtractDocuments(conversation));
        }

        return loaded;
    }

    private static IEnumerable<EpisodicMemoryDocument> ExtractDocuments(Conversation conversation)
    {
        foreach (var message in conversation.Messages)
        {
            if (message.Role != MessageRole.System || !message.Content.StartsWith(EpisodicMarker, StringComparison.Ordinal))
            {
                continue;
            }

            var payload = message.Content[EpisodicMarker.Length..];
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            EpisodicMemoryDocument? parsed = null;

            try
            {
                parsed = JsonSerializer.Deserialize<EpisodicMemoryDocument>(payload, JsonOptions);
            }
            catch
            {
                // Ignore malformed payload entries.
            }

            if (parsed is not null)
            {
                yield return parsed;
            }
        }
    }

    private static IEnumerable<Guid> EnumeratePossibleConversationIds(KnowledgeSearchResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.EntityId) && Guid.TryParse(result.EntityId, out var parsedEntityId))
        {
            yield return parsedEntityId;
        }

        if (Guid.TryParse(result.ChunkText, out var parsedChunk))
        {
            yield return parsedChunk;
        }
    }

    private float SimilarityForDocument(EpisodicMemoryDocument document, float[] queryEmbedding)
    {
        if (document.EmbeddingVector is not { Length: > 0 })
        {
            return 0f;
        }

        if (document.EmbeddingVector.Length != _options.EmbeddingDimension)
        {
            return 0f;
        }

        return CosineSimilarity(queryEmbedding, document.EmbeddingVector);
    }

    private void EnforceUserLimit(string userId)
    {
        var userDocuments = CacheByDocumentId.Values
            .Where(x => x.UserId.Equals(userId, StringComparison.Ordinal))
            .OrderByDescending(x => x.EndedAt)
            .ToList();

        if (userDocuments.Count <= _options.MaxEpisodesPerUser)
        {
            return;
        }

        foreach (var document in userDocuments.Skip(_options.MaxEpisodesPerUser))
        {
            CacheByDocumentId.Remove(document.Id);
        }
    }

    private static string BuildFallbackSummary(Conversation conversation, IReadOnlyList<Message> messages)
    {
        var latestUserMessage = messages
            .Where(m => m.Role == MessageRole.User)
            .Select(m => m.Content)
            .LastOrDefault(m => !string.IsNullOrWhiteSpace(m));

        if (!string.IsNullOrWhiteSpace(latestUserMessage))
        {
            return $"Conversation '{conversation.Title}' focused on: {latestUserMessage.Trim()}";
        }

        return $"Conversation '{conversation.Title}' contained {messages.Count} messages.";
    }

    private static List<string> ExtractFallbackTopics(IReadOnlyList<Message> messages)
    {
        return messages
            .SelectMany(m => m.Content.Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?'], StringSplitOptions.RemoveEmptyEntries))
            .Where(x => x.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return (text.Length + 3) / 4;
    }

    private static float CosineSimilarity(float[] left, float[] right)
    {
        var dimensions = Math.Min(left.Length, right.Length);
        if (dimensions == 0)
        {
            return 0f;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var i = 0; i < dimensions; i++)
        {
            var l = left[i];
            var r = right[i];

            dot += l * r;
            leftNorm += l * l;
            rightNorm += r * r;
        }

        if (leftNorm == 0 || rightNorm == 0)
        {
            return 0f;
        }

        return (float)(dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)));
    }

    internal sealed class EpisodicMemoryDocument
    {
        public Guid Id { get; init; }
        public string ExternalEpisodeId { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public string ConversationId { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public IReadOnlyList<string> KeyTopics { get; init; } = [];
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset EndedAt { get; init; }
        public int MessageCount { get; init; }
        public float[]? EmbeddingVector { get; init; }

        public static EpisodicMemoryDocument FromEpisode(Episode episode, float[]? embedding) => new()
        {
            Id = Guid.NewGuid(),
            ExternalEpisodeId = episode.Id,
            UserId = episode.UserId,
            ConversationId = episode.ConversationId,
            Summary = episode.Summary,
            KeyTopics = episode.KeyTopics.ToArray(),
            StartedAt = episode.StartedAt,
            EndedAt = episode.EndedAt,
            MessageCount = episode.MessageCount,
            EmbeddingVector = embedding
        };

        public Episode ToEpisode() => new(
            Id: ExternalEpisodeId,
            UserId: UserId,
            ConversationId: ConversationId,
            Summary: Summary,
            KeyTopics: KeyTopics,
            StartedAt: StartedAt,
            EndedAt: EndedAt,
            MessageCount: MessageCount);
    }

    private sealed record EpisodeSearchHit(EpisodicMemoryDocument Document, float Score);
}
