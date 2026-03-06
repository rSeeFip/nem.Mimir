using System.Text.Json;
using MediatR;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Application.ChannelEvents;
using Mimir.Teams.Configuration;
using Mimir.Teams.Telemetry;
using nem.Contracts.Channels;
using nem.Contracts.Content;

namespace Mimir.Teams.Services;

internal sealed class TeamsChannelAdapter :
    IChannelEventSource,
    Mimir.Application.ChannelEvents.IChannelMessageSender,
    IBot
{
    private readonly TeamsSettings _settings;
    private readonly IMediator _mediator;
    private readonly ActivityConverter _activityConverter;
    private readonly AdaptiveCardBuilder _cardBuilder;
    private readonly ILogger<TeamsChannelAdapter> _logger;

    internal AadClaimMapper ClaimMapper { get; }

    public TeamsChannelAdapter(
        IOptions<TeamsSettings> settings,
        IMediator mediator,
        ActivityConverter activityConverter,
        AdaptiveCardBuilder cardBuilder,
        AadClaimMapper claimMapper,
        ILogger<TeamsChannelAdapter> logger)
    {
        _settings = settings.Value;
        _mediator = mediator;
        _activityConverter = activityConverter;
        _cardBuilder = cardBuilder;
        ClaimMapper = claimMapper;
        _logger = logger;
    }

    public ChannelType Channel => ChannelType.Teams;

    public event Func<ChannelEvent, Task>? OnEventReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.AppId))
        {
            _logger.LogWarning("Teams App ID is not configured. Adapter will not start.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Teams channel adapter started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Teams channel adapter stopped");
        return Task.CompletedTask;
    }

    public Task<SendChannelMessageResult> SendAsync(
        string externalChannelId,
        IContentPayload content,
        CancellationToken ct)
    {
        using var activity = TeamsActivitySource.StartClientActivity("Teams.SendMessage");

        try
        {
            _logger.LogInformation("Sending message to Teams conversation {ConversationId}", externalChannelId);

            var cardJson = _cardBuilder.Build(content);
            _logger.LogDebug("Built adaptive card ({Length} chars) for conversation {ConversationId}", cardJson.Length, externalChannelId);

            return Task.FromResult(new SendChannelMessageResult(
                Success: true,
                MessageRef: new ChannelMessageRef(ChannelType.Teams, Guid.NewGuid().ToString()),
                ErrorMessage: null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Teams conversation {ConversationId}", externalChannelId);
            return Task.FromResult(new SendChannelMessageResult(
                Success: false,
                MessageRef: null,
                ErrorMessage: ex.Message));
        }
    }

    public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        using var traceActivity = TeamsActivitySource.StartServerActivity("Teams.OnTurn");

        var incomingActivity = turnContext.Activity;

        if (incomingActivity.Type != ActivityTypes.Message)
        {
            _logger.LogDebug("Ignoring non-message activity type {ActivityType}", incomingActivity.Type);
            return;
        }

        var conversationId = incomingActivity.Conversation?.Id ?? "unknown";
        var userId = incomingActivity.From?.Id ?? "unknown";
        var timestamp = incomingActivity.Timestamp ?? DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Received Teams message from {UserId} in conversation {ConversationId}",
            userId,
            conversationId);

        var contentPayload = _activityConverter.Convert(incomingActivity);

        var channelEvent = new ChannelEvent
        {
            Channel = ChannelType.Teams,
            EventType = "message",
            Timestamp = timestamp,
            Payload = JsonSerializer.SerializeToElement(new
            {
                text = incomingActivity.Text,
                from = incomingActivity.From?.Name,
                conversationId,
            }),
        };

        if (OnEventReceived is { } handler)
        {
            await handler(channelEvent);
        }

        var command = new IngestChannelEventCommand(
            Channel: ChannelType.Teams,
            ExternalChannelId: conversationId,
            ExternalUserId: userId,
            Content: contentPayload,
            Timestamp: timestamp);

        await _mediator.Send(command, cancellationToken);
    }
}
