namespace nem.Mimir.Infrastructure.Knowledge;

using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Knowledge;

internal sealed class KnowHubIngestionService(ILogger<KnowHubIngestionService> logger) : IKnowledgeIngestionService
{
    public Task IngestDocumentAsync(Guid documentId, string fileName, string contentUrl, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Queuing document ingestion to KnowHub. DocumentId={DocumentId}, FileName={FileName}, ContentUrl={ContentUrl}",
            documentId,
            fileName,
            contentUrl);

        return Task.CompletedTask;
    }
}
