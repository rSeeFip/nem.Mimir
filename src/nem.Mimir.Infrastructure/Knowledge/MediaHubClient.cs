namespace nem.Mimir.Infrastructure.Knowledge;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Knowledge;

internal sealed class MediaHubClient(
    IOptions<MediaHubOptions> options,
    ILogger<MediaHubClient> logger) : IMediaHubClient
{
    public Task<MediaHubUploadResult> UploadAsync(
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken ct = default)
    {
        var fileId = Guid.NewGuid();

        if (!options.Value.Enabled)
        {
            logger.LogInformation("MediaHub is disabled. Returning stub upload URL for file {FileName}.", fileName);
            return Task.FromResult(new MediaHubUploadResult(
                fileId,
                $"mediahub://stub/{fileId}/{Uri.EscapeDataString(fileName)}",
                contentType,
                content.LongLength));
        }

        logger.LogInformation("MediaHub direct upload API is not integrated yet. Returning stub upload URL for file {FileName}.", fileName);
        return Task.FromResult(new MediaHubUploadResult(
            fileId,
            $"mediahub://pending/{fileId}/{Uri.EscapeDataString(fileName)}",
            contentType,
            content.LongLength));
    }
}
