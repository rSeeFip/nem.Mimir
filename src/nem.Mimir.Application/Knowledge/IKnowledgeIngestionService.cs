namespace nem.Mimir.Application.Knowledge;

public interface IKnowledgeIngestionService
{
    Task IngestDocumentAsync(Guid documentId, string fileName, string contentUrl, CancellationToken ct = default);
}
