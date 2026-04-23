namespace nem.Mimir.Application.Common.Models;

using nem.Mimir.Application.Knowledge;

public sealed record KnowledgeDocumentDto(
    Guid DocumentId,
    string FileName,
    string StorageUrl,
    string? ContentType,
    DateTimeOffset AddedAt);

public sealed record KnowledgeCollectionDto(
    Guid Id,
    Guid UserId,
    string Name,
    string Description,
    IReadOnlyList<KnowledgeDocumentDto> Documents,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record KnowledgeSearchResultDto(
    Guid DocumentId,
    string ChunkText,
    float Similarity,
    string? EntityType,
    string? EntityId,
    SourceOriginLinkDto? OriginLink = null);

public sealed record WebSearchResultDto(
    string Title,
    string Url,
    string? Snippet,
    string? Source,
    DateTimeOffset? PublishedAt);

public sealed record ChatFileUploadDto(
    Guid DocumentId,
    string FileName,
    string StorageUrl,
    Guid? KnowledgeCollectionId,
    string IndexingStatus);
