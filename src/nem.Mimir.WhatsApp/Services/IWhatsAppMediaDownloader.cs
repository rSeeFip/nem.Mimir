namespace nem.Mimir.WhatsApp.Services;

/// <summary>
/// Abstraction for downloading media files from WhatsApp CDN.
/// </summary>
internal interface IWhatsAppMediaDownloader
{
    /// <summary>
    /// Retrieves the download URL for a WhatsApp media ID.
    /// </summary>
    Task<string?> GetMediaUrlAsync(string mediaId, CancellationToken ct);

    /// <summary>
    /// Downloads the media content as a byte array.
    /// </summary>
    Task<byte[]?> DownloadMediaAsync(string mediaUrl, CancellationToken ct);
}
