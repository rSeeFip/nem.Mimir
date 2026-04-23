namespace nem.Mimir.Application.Knowledge;

public interface IKnowHubBridge
{
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchKnowledgeAsync(string query, int maxResults = 10, CancellationToken ct = default);
    Task<IReadOnlyList<DistilledKnowledge>> DistillKnowledgeAsync(string content, string source, CancellationToken ct = default);
    Task<GraphQueryResult> QueryGraphAsync(string query, string tenantId, int maxResults = 10, CancellationToken ct = default);
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    bool IsAvailable { get; }
}

public sealed record KnowledgeSearchResult(
    Guid ContentId,
    string ChunkText,
    float Similarity,
    string? EntityType = null,
    string? EntityId = null,
    SourceOriginLinkDto? OriginLink = null);

public sealed record SourceOriginLinkDto(
    string MediaHubUrl,
    string OriginType,
    string DisplayName,
    int? LineStart = null,
    int? LineEnd = null,
    string? ThumbnailUrl = null)
{
    public string ToMarkdown() =>
        OriginType switch
        {
            "Cad" => $"[📐 {DisplayName}]({MediaHubUrl})",
            "Code" => LineStart.HasValue
                ? $"[💻 {DisplayName}:{LineStart}]({MediaHubUrl})"
                : $"[💻 {DisplayName}]({MediaHubUrl})",
            "Audio" => $"[🎙️ {DisplayName}]({MediaHubUrl})",
            "Document" => $"[📄 {DisplayName}]({MediaHubUrl})",
            _ => $"[📎 {DisplayName}]({MediaHubUrl})",
        };
}

public sealed record DistilledKnowledge(
    Guid? KnowledgeId,
    string Outcome,
    double CompositeScore,
    string Type,
    string Content);

public sealed record GraphQueryResult(
    string Answer,
    string Mode,
    IReadOnlyList<string> RelevantEntities,
    IReadOnlyList<string> RelevantCommunities,
    IReadOnlyList<string> ContextChunks,
    double Score);
