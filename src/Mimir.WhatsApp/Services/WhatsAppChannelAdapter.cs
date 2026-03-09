namespace Mimir.WhatsApp.Services;

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Application.ChannelEvents;
using Mimir.WhatsApp.Configuration;
using Mimir.WhatsApp.Models;
using nem.Contracts.Channels;
using nem.Contracts.Content;

internal sealed class WhatsAppChannelAdapter(
    IOptions<WhatsAppSettings> settings,
    IHttpClientFactory httpClientFactory,
    ISender mediator,
    ILogger<WhatsAppChannelAdapter> logger) : BackgroundService, IChannelEventSource, Mimir.Application.ChannelEvents.IChannelMessageSender
{
    internal static readonly ActivitySource ActivitySource = new("Mimir.WhatsApp");
    internal const string HttpClientName = "WhatsAppApi";

    private event Func<ChannelEvent, Task>? _onEventReceived;

    public ChannelType Channel => ChannelType.WhatsApp;

    event Func<ChannelEvent, Task> IChannelEventSource.OnEventReceived
    {
        add => _onEventReceived += value;
        remove => _onEventReceived -= value;
    }

    Task IChannelEventSource.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IChannelEventSource.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Value.AccessToken))
        {
            logger.LogError("WhatsApp access token is not configured. Adapter will not start.");
            return Task.CompletedTask;
        }

        logger.LogInformation("WhatsApp channel adapter started for phone number ID {PhoneNumberId}",
            settings.Value.PhoneNumberId);

        return Task.CompletedTask;
    }

    internal async Task ProcessWebhookPayloadAsync(WhatsAppWebhookPayload payload, CancellationToken ct)
    {
        if (payload.Entry is null)
            return;

        foreach (var entry in payload.Entry)
        {
            if (entry.Changes is null)
                continue;

            foreach (var change in entry.Changes)
            {
                if (change.Value?.Messages is null)
                    continue;

                foreach (var message in change.Value.Messages)
                {
                    await ProcessMessageAsync(message, ct);
                }
            }
        }
    }

    private async Task ProcessMessageAsync(WhatsAppMessage message, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("WhatsApp.ProcessMessage");
        activity?.SetTag("whatsapp.message_id", message.Id);
        activity?.SetTag("whatsapp.message_type", message.Type);
        activity?.SetTag("whatsapp.sender", message.From);

        try
        {
            var content = WhatsAppMessageConverter.ToContentPayload(message);
            var externalUserId = message.From ?? "unknown";
            var externalChannelId = message.From ?? "unknown";
            var timestamp = ParseMessageTimestamp(message.Timestamp);

            var command = new IngestChannelEventCommand(
                ChannelType.WhatsApp,
                externalChannelId,
                externalUserId,
                content,
                timestamp);

            var result = await mediator.Send(command, ct);

            logger.LogInformation(
                "Ingested WhatsApp message {MessageId} from {Sender}, EventId={EventId}",
                message.Id, message.From, result.EventId);

            if (_onEventReceived is not null)
            {
                var channelEvent = new ChannelEvent
                {
                    Channel = ChannelType.WhatsApp,
                    EventType = "message",
                    Timestamp = timestamp,
                    Payload = JsonSerializer.SerializeToElement(message),
                };
                await _onEventReceived.Invoke(channelEvent);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing WhatsApp message {MessageId}", message.Id);
        }
    }

    public async Task<SendChannelMessageResult> SendAsync(string externalChannelId, IContentPayload content, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("WhatsApp.SendMessage");
        activity?.SetTag("whatsapp.recipient", externalChannelId);

        var textContent = content as TextContent;
        if (textContent is null)
        {
            return new SendChannelMessageResult(false, null, "Only text messages are currently supported for WhatsApp outbound.");
        }

        var request = new WhatsAppSendMessageRequest
        {
            To = externalChannelId,
            Type = "text",
            Text = new WhatsAppSendTextBody { Body = textContent.Text },
        };

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var url = $"{settings.Value.ApiBaseUrl}/{settings.Value.PhoneNumberId}/messages";

            var response = await client.PostAsJsonAsync(url, request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<WhatsAppSendMessageResponse>(ct);
            var messageId = result?.Messages?.FirstOrDefault()?.Id;

            return new SendChannelMessageResult(
                true,
                messageId is not null ? new ChannelMessageRef(ChannelType.WhatsApp, messageId) : null,
                null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp message to {Recipient}", externalChannelId);
            return new SendChannelMessageResult(false, null, ex.Message);
        }
    }

    private static DateTimeOffset ParseMessageTimestamp(string? timestamp)
    {
        if (!string.IsNullOrEmpty(timestamp) && long.TryParse(timestamp, out var unixSeconds))
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        return DateTimeOffset.UtcNow;
    }
}
