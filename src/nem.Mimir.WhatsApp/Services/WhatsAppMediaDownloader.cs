namespace nem.Mimir.WhatsApp.Services;

using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.WhatsApp.Configuration;
using nem.Mimir.WhatsApp.Models;

internal sealed class WhatsAppMediaDownloader(
    IHttpClientFactory httpClientFactory,
    IOptions<WhatsAppSettings> settings,
    ILogger<WhatsAppMediaDownloader> logger) : IWhatsAppMediaDownloader
{
    internal const string HttpClientName = "WhatsAppMedia";

    public async Task<string?> GetMediaUrlAsync(string mediaId, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var url = $"{settings.Value.ApiBaseUrl}/{mediaId}";

        try
        {
            var response = await client.GetFromJsonAsync<WhatsAppMediaUrlResponse>(url, ct);
            return response?.Url;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve media URL for {MediaId}", mediaId);
            return null;
        }
    }

    public async Task<byte[]?> DownloadMediaAsync(string mediaUrl, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            return await client.GetByteArrayAsync(mediaUrl, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download media from {MediaUrl}", mediaUrl);
            return null;
        }
    }
}
