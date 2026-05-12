namespace nem.Mimir.Signal.Services;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Signal.Configuration;

internal sealed class SignalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly SignalSettings _settings;
    private readonly ILogger<SignalApiClient> _logger;

    public SignalApiClient(
        HttpClient httpClient,
        IOptions<SignalSettings> settings,
        ILogger<SignalApiClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SignalReceivedMessage>> ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        using var activity = SignalActivitySource.Instance.StartActivity("signal.receive");
        activity?.SetTag("signal.phone", _settings.PhoneNumber);

        var encodedPhone = Uri.EscapeDataString(_settings.PhoneNumber);
        var response = await _httpClient.GetAsync($"/v1/receive/{encodedPhone}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var messages = await response.Content.ReadFromJsonAsync<List<SignalReceivedMessage>>(
            JsonOptions.Default,
            cancellationToken) ?? [];

        _logger.LogDebug("Received {Count} messages from Signal", messages.Count);
        return messages;
    }

    public async Task<bool> SendMessageAsync(string recipient, string messageBody, CancellationToken cancellationToken)
    {
        using var activity = SignalActivitySource.Instance.StartActivity("signal.send");
        activity?.SetTag("signal.recipient", recipient);

        var payload = new SignalSendRequest
        {
            Number = _settings.PhoneNumber,
            Recipients = [recipient],
            Message = messageBody,
        };

        var response = await _httpClient.PostAsJsonAsync("/v2/send", payload, JsonOptions.Default, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to send Signal message to {Recipient}: {Status}", recipient, response.StatusCode);
            return false;
        }

        return true;
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/v1/about", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signal API health check failed");
            return false;
        }
    }
}

internal sealed record SignalReceivedMessage
{
    [JsonPropertyName("envelope")]
    public SignalEnvelope? Envelope { get; init; }
}

internal sealed record SignalEnvelope
{
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("sourceUuid")]
    public string? SourceUuid { get; init; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }

    [JsonPropertyName("dataMessage")]
    public SignalDataMessage? DataMessage { get; init; }
}

internal sealed record SignalDataMessage
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }

    [JsonPropertyName("attachments")]
    public List<SignalAttachment>? Attachments { get; init; }
}

internal sealed record SignalAttachment
{
    [JsonPropertyName("contentType")]
    public string? ContentType { get; init; }

    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("size")]
    public long? Size { get; init; }
}

internal sealed record SignalSendRequest
{
    [JsonPropertyName("number")]
    public required string Number { get; init; }

    [JsonPropertyName("recipients")]
    public required string[] Recipients { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
