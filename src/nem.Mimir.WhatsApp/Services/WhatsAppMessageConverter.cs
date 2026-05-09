namespace nem.Mimir.WhatsApp.Services;

using nem.Contracts.Content;
using nem.Mimir.WhatsApp.Models;

internal static class WhatsAppMessageConverter
{
    public static IContentPayload ToContentPayload(WhatsAppMessage message)
    {
        return message.Type switch
        {
            "text" => new TextContent(message.Text?.Body ?? string.Empty)
            {
                CreatedAt = ParseTimestamp(message.Timestamp),
            },
            "audio" => new VoiceContent(
                TranscriptionText: null,
                AudioUrl: null,
                DurationSeconds: null,
                MimeType: message.Audio?.MimeType)
            {
                CreatedAt = ParseTimestamp(message.Timestamp),
            },
            "image" => new TextContent(
                Text: message.Image?.Caption ?? "[Image received]",
                Format: "whatsapp-image")
            {
                CreatedAt = ParseTimestamp(message.Timestamp),
            },
            "video" => new TextContent(
                Text: message.Video?.Caption ?? "[Video received]",
                Format: "whatsapp-video")
            {
                CreatedAt = ParseTimestamp(message.Timestamp),
            },
            "document" => new TextContent(
                Text: message.Document?.Caption ?? $"[Document: {message.Document?.Filename ?? "unknown"}]",
                Format: "whatsapp-document")
            {
                CreatedAt = ParseTimestamp(message.Timestamp),
            },
            _ => new TextContent($"[Unsupported message type: {message.Type}]")
            {
                CreatedAt = ParseTimestamp(message.Timestamp),
            },
        };
    }

    private static DateTimeOffset? ParseTimestamp(string? timestamp)
    {
        if (string.IsNullOrEmpty(timestamp))
            return null;

        if (long.TryParse(timestamp, out var unixSeconds))
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        return null;
    }
}
