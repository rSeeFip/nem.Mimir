namespace nem.Mimir.Api.Hubs;

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using nem.Contracts.Channels;
using nem.Contracts.Content;

/// <summary>
/// Channel adapter that bridges SignalR WebWidget clients with the nem channel abstraction.
/// Wraps the existing <see cref="ChatHub"/> — does NOT replace it.
/// </summary>
public sealed class WebWidgetChannelAdapter : BackgroundService, IChannelEventSource, IChannelMessageSender
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<WebWidgetChannelAdapter> _logger;

    public WebWidgetChannelAdapter(
        IHubContext<ChatHub> hubContext,
        ILogger<WebWidgetChannelAdapter> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public ChannelType Channel => ChannelType.WebWidget;

    public ChannelCapabilities Capabilities =>
        ChannelCapabilities.Text | ChannelCapabilities.StreamingUpdates;

    public event Func<ChannelEvent, Task>? OnEventReceived;

    Task IChannelEventSource.StartAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken);

    Task IChannelEventSource.StopAsync(CancellationToken cancellationToken) => StopAsync(cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebWidget channel adapter started");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }

        _logger.LogInformation("WebWidget channel adapter stopped");
    }

    /// <summary>
    /// Called by ChatHub (or middleware) when a SignalR client sends an inbound message.
    /// Converts to a <see cref="ChannelEvent"/> and raises <see cref="OnEventReceived"/>.
    /// </summary>
    public async Task HandleInboundMessage(string connectionId, string userId, string conversationId, string text)
    {
        using var activity = WebWidgetActivitySource.Instance.StartActivity("webwidget.channel.receive");
        activity?.SetTag("webwidget.user_id", userId);
        activity?.SetTag("webwidget.connection_id", connectionId);

        var timestamp = DateTimeOffset.UtcNow;

        var metadata = new Dictionary<string, string>
        {
            ["webwidget.connection_id"] = connectionId,
            ["webwidget.conversation_id"] = conversationId,
        };

        var inbound = new InboundChannelMessage
        {
            Channel = ChannelType.WebWidget,
            ChannelUserId = userId,
            Text = text,
            Timestamp = timestamp,
            Metadata = metadata,
        };

        var channelEvent = new ChannelEvent
        {
            Channel = ChannelType.WebWidget,
            EventType = "message",
            Timestamp = timestamp,
            Payload = JsonSerializer.SerializeToElement(inbound),
        };

        if (OnEventReceived is { } handler)
        {
            try
            {
                await handler(channelEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling inbound WebWidget event for user {UserId}", userId);
            }
        }
    }

    public async Task<MessageDeliveryResult> SendMessageAsync(
        OutboundChannelMessage message,
        CancellationToken cancellationToken)
    {
        using var activity = WebWidgetActivitySource.Instance.StartActivity("webwidget.channel.send");
        activity?.SetTag("webwidget.target_user", message.TargetUserId);

        try
        {
            var (text, blocksJson) = FormatOutboundPayloadRef(message.ContentPayloadRef);

            await _hubContext.Clients
                .User(message.TargetUserId)
                .SendAsync(
                    "ReceiveMessage",
                    new { text, blocks = blocksJson },
                    cancellationToken);

            var messageRef = new ChannelMessageRef(ChannelType.WebWidget, Guid.NewGuid().ToString());

            return new MessageDeliveryResult
            {
                Success = true,
                MessageRef = messageRef,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to WebWidget user {TargetUserId}",
                message.TargetUserId);

            return new MessageDeliveryResult
            {
                Success = false,
                ErrorMessage = $"Failed to send message via WebWidget: {ex.Message}",
            };
        }
    }

    public async Task SendStreamingAsync(
        OutboundChannelMessage message,
        IAsyncEnumerable<string> chunks,
        CancellationToken cancellationToken)
    {
        using var activity = WebWidgetActivitySource.Instance.StartActivity("webwidget.channel.stream");
        activity?.SetTag("webwidget.target_user", message.TargetUserId);

        var client = _hubContext.Clients.User(message.TargetUserId);

        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            var token = new ChatToken(chunk, false, null);
            await client.SendAsync("ReceiveToken", token, cancellationToken);
        }
    }

    /// <summary>
    /// Extracts an <see cref="ActorIdentity"/> from Keycloak JWT claims.
    /// </summary>
    public static ActorIdentity? ExtractActorIdentity(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        var userId = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is null)
            return null;

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        var displayName = principal.FindFirst(ClaimTypes.Name)?.Value;

        return new ActorIdentity(userId, email, displayName);
    }

    /// <summary>
    /// Converts an inbound SignalR message text to an <see cref="IContentPayload"/>.
    /// Attempts JSON deserialization first; falls back to plain text.
    /// </summary>
    public static IContentPayload ConvertToContentPayload(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return new TextContent(string.Empty);

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.TryGetProperty("contentType", out var contentTypeProp)
                && contentTypeProp.GetString() is "text"
                && root.TryGetProperty("text", out var textProp))
            {
                return new TextContent(textProp.GetString() ?? string.Empty);
            }

            // If it's valid JSON with contentType, try full deserialization
            if (root.TryGetProperty("contentType", out _))
            {
                var payload = JsonSerializer.Deserialize<IContentPayload>(text);
                if (payload is not null)
                    return payload;
            }
        }
        catch (JsonException)
        {
            // Not valid JSON — fall through to plain text
        }

        return new TextContent(text);
    }

    /// <summary>
    /// Formats an <see cref="IContentPayload"/> for SignalR delivery.
    /// Returns both plain text (backward compat) and optional ContentBlock[] JSON.
    /// </summary>
    public static (string Text, string? BlocksJson) FormatOutboundPayload(IContentPayload? payload)
    {
        if (payload is null)
            return (string.Empty, null);

        if (payload is TextContent textContent)
            return (textContent.Text, null);

        var json = JsonSerializer.Serialize(payload);
        return (json, null);
    }

    /// <summary>
    /// Formats a raw content payload ref string for outbound delivery.
    /// </summary>
    public static (string Text, string? BlocksJson) FormatOutboundPayloadRef(string? contentPayloadRef)
    {
        if (string.IsNullOrEmpty(contentPayloadRef))
            return (string.Empty, null);

        return (contentPayloadRef, null);
    }
}
