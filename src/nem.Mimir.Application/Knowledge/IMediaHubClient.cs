namespace nem.Mimir.Application.Knowledge;

public interface IMediaHubClient
{
    Task<MediaHubUploadResult> UploadAsync(
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken ct = default);
}

public sealed record MediaHubUploadResult(
    Guid FileId,
    string Url,
    string ContentType,
    long SizeBytes);
