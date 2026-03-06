namespace Mimir.Signal.Services;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Signal.Configuration;
using nem.Contracts.Channels;
using nem.Contracts.Content;

internal sealed class SignalChannelAdapter : BackgroundService, IChannelEventSource, IChannelMessageSender
{
    private readonly SignalApiClient _apiClient;
    private readonly SignalSettings _settings;
    private readonly ILogger<SignalChannelAdapter> _logger;

    public SignalChannelAdapter(
        SignalApiClient apiClient,
        IOptions<SignalSettings> settings,
        ILogger<SignalChannelAdapter> logger)
    {
        _apiClient = apiClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public ChannelType Channel => ChannelType.Signal;

    public ChannelCapabilities Capabilities => ChannelCapabilities.Text | ChannelCapabilities.FileAttachments;

    public event Func<ChannelEvent, Task>? OnEventReceived;

    Task IChannelEventSource.StartAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken);

    Task IChannelEventSource.StopAsync(CancellationToken cancellationToken) => StopAsync(cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.PhoneNumber))
        {
            _logger.LogError("Signal phone number is not configured. Adapter will not start.");
            return;
        }

        _logger.LogInformation("Starting Signal channel adapter with polling for {Phone}...", _settings.PhoneNumber);

        var healthy = await _apiClient.CheckHealthAsync(stoppingToken);
        if (!healthy)
        {
            _logger.LogError("signal-cli-rest-api is not reachable. Adapter will not start.");
            return;
        }

        _logger.LogInformation("Signal channel adapter started successfully");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _apiClient.ReceiveMessagesAsync(stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        await ProcessMessageAsync(message, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing Signal message from {Source}",
                            message.Envelope?.SourceUuid ?? "unknown");
                    }

                    if (stoppingToken.IsCancellationRequested) break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(_settings.PollingIntervalMs), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Signal channel adapter shutting down gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Signal polling loop. Retrying in 5 seconds...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Signal channel adapter stopped");
    }

    public async Task<MessageDeliveryResult> SendMessageAsync(OutboundChannelMessage message, CancellationToken cancellationToken)
    {
        using var activity = SignalActivitySource.Instance.StartActivity("signal.channel.send");
        activity?.SetTag("signal.recipient", message.TargetUserId);

        var text = message.ContentPayloadRef ?? string.Empty;
        var success = await _apiClient.SendMessageAsync(message.TargetUserId, text, cancellationToken);

        return new MessageDeliveryResult
        {
            Success = success,
            MessageRef = success
                ? new ChannelMessageRef(ChannelType.Signal, Guid.NewGuid().ToString())
                : null,
            ErrorMessage = success ? null : "Failed to send message via Signal API",
        };
    }

    private async Task ProcessMessageAsync(SignalReceivedMessage received, CancellationToken cancellationToken)
    {
        var envelope = received.Envelope;
        if (envelope?.DataMessage is null) return;

        var senderUuid = envelope.SourceUuid ?? envelope.Source ?? "unknown";
        var text = envelope.DataMessage.Message;

        using var activity = SignalActivitySource.Instance.StartActivity("signal.channel.receive");
        activity?.SetTag("signal.sender", senderUuid);

        var timestamp = envelope.Timestamp.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(envelope.Timestamp.Value)
            : DateTimeOffset.UtcNow;

        var metadata = BuildMetadata(envelope);

        var channelEvent = new ChannelEvent
        {
            Channel = ChannelType.Signal,
            EventType = "message",
            Timestamp = timestamp,
            Payload = JsonSerializer.SerializeToElement(new InboundChannelMessage
            {
                Channel = ChannelType.Signal,
                ChannelUserId = senderUuid,
                Text = text,
                Timestamp = timestamp,
                Metadata = metadata,
            }),
        };

        if (OnEventReceived is { } handler)
        {
            await handler(channelEvent);
        }
    }

    private static Dictionary<string, string>? BuildMetadata(SignalEnvelope envelope)
    {
        var metadata = new Dictionary<string, string>();

        if (envelope.Source is not null)
            metadata["signal.source_phone"] = envelope.Source;

        if (envelope.SourceUuid is not null)
            metadata["signal.source_uuid"] = envelope.SourceUuid;

        if (envelope.DataMessage?.Attachments is { Count: > 0 } attachments)
        {
            metadata["signal.attachment_count"] = attachments.Count.ToString();
            for (var i = 0; i < attachments.Count; i++)
            {
                var att = attachments[i];
                if (att.ContentType is not null)
                    metadata[$"signal.attachment_{i}_type"] = att.ContentType;
                if (att.Filename is not null)
                    metadata[$"signal.attachment_{i}_name"] = att.Filename;
                if (att.Id is not null)
                    metadata[$"signal.attachment_{i}_id"] = att.Id;
            }
        }

        return metadata.Count > 0 ? metadata : null;
    }
}
