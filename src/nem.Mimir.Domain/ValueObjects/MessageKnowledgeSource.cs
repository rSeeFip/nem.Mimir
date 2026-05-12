namespace nem.Mimir.Domain.ValueObjects;

public sealed record MessageKnowledgeSource(
    Guid DocumentId,
    string ChunkText,
    float Similarity,
    string? EntityType,
    string? EntityId);
