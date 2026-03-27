using nem.Contracts.Memory;

namespace nem.Mimir.Application.Services.Memory;

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

internal sealed record EpisodeSearchHit(EpisodicMemoryDocument Document, float Score);
