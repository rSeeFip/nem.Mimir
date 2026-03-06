namespace Mimir.WhatsApp.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Root payload received from WhatsApp Cloud API webhook.
/// </summary>
internal sealed record WhatsAppWebhookPayload
{
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    [JsonPropertyName("entry")]
    public IReadOnlyList<WhatsAppEntry>? Entry { get; init; }
}

internal sealed record WhatsAppEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("changes")]
    public IReadOnlyList<WhatsAppChange>? Changes { get; init; }
}

internal sealed record WhatsAppChange
{
    [JsonPropertyName("value")]
    public WhatsAppChangeValue? Value { get; init; }

    [JsonPropertyName("field")]
    public string? Field { get; init; }
}

internal sealed record WhatsAppChangeValue
{
    [JsonPropertyName("messaging_product")]
    public string? MessagingProduct { get; init; }

    [JsonPropertyName("metadata")]
    public WhatsAppMetadata? Metadata { get; init; }

    [JsonPropertyName("contacts")]
    public IReadOnlyList<WhatsAppContact>? Contacts { get; init; }

    [JsonPropertyName("messages")]
    public IReadOnlyList<WhatsAppMessage>? Messages { get; init; }

    [JsonPropertyName("statuses")]
    public IReadOnlyList<WhatsAppStatus>? Statuses { get; init; }
}

internal sealed record WhatsAppMetadata
{
    [JsonPropertyName("display_phone_number")]
    public string? DisplayPhoneNumber { get; init; }

    [JsonPropertyName("phone_number_id")]
    public string? PhoneNumberId { get; init; }
}

internal sealed record WhatsAppContact
{
    [JsonPropertyName("profile")]
    public WhatsAppProfile? Profile { get; init; }

    [JsonPropertyName("wa_id")]
    public string? WaId { get; init; }
}

internal sealed record WhatsAppProfile
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed record WhatsAppMessage
{
    [JsonPropertyName("from")]
    public string? From { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("text")]
    public WhatsAppTextBody? Text { get; init; }

    [JsonPropertyName("image")]
    public WhatsAppMediaBody? Image { get; init; }

    [JsonPropertyName("audio")]
    public WhatsAppMediaBody? Audio { get; init; }

    [JsonPropertyName("video")]
    public WhatsAppMediaBody? Video { get; init; }

    [JsonPropertyName("document")]
    public WhatsAppDocumentBody? Document { get; init; }
}

internal sealed record WhatsAppTextBody
{
    [JsonPropertyName("body")]
    public string? Body { get; init; }
}

internal sealed record WhatsAppMediaBody
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }
}

internal sealed record WhatsAppDocumentBody
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }

    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }
}

internal sealed record WhatsAppStatus
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("recipient_id")]
    public string? RecipientId { get; init; }
}

/// <summary>
/// WhatsApp Cloud API send message request.
/// </summary>
internal sealed record WhatsAppSendMessageRequest
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; init; } = "whatsapp";

    [JsonPropertyName("to")]
    public required string To { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("text")]
    public WhatsAppSendTextBody? Text { get; init; }
}

internal sealed record WhatsAppSendTextBody
{
    [JsonPropertyName("body")]
    public required string Body { get; init; }
}

/// <summary>
/// WhatsApp Cloud API send message response.
/// </summary>
internal sealed record WhatsAppSendMessageResponse
{
    [JsonPropertyName("messaging_product")]
    public string? MessagingProduct { get; init; }

    [JsonPropertyName("contacts")]
    public IReadOnlyList<WhatsAppSendContact>? Contacts { get; init; }

    [JsonPropertyName("messages")]
    public IReadOnlyList<WhatsAppSendMessageRef>? Messages { get; init; }
}

internal sealed record WhatsAppSendContact
{
    [JsonPropertyName("input")]
    public string? Input { get; init; }

    [JsonPropertyName("wa_id")]
    public string? WaId { get; init; }
}

internal sealed record WhatsAppSendMessageRef
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

/// <summary>
/// WhatsApp Cloud API media URL response.
/// </summary>
internal sealed record WhatsAppMediaUrlResponse
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }
}
