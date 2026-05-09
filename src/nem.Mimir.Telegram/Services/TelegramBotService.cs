using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using nem.Mimir.Telegram.Configuration;
using nem.Mimir.Telegram.Handlers;
using nem.Mimir.Telegram.Voice;

namespace nem.Mimir.Telegram.Services;

/// <summary>
/// Background service that runs the Telegram bot with long-polling.
/// </summary>
internal sealed class TelegramBotService : BackgroundService
{
    private readonly TelegramSettings _settings;
    private readonly ICommandHandler _commandHandler;
    private readonly IMessageHandler _messageHandler;
    private readonly IVoiceMessageHandler? _voiceMessageHandler;
    private readonly ILogger<TelegramBotService> _logger;
    private ITelegramBotClient? _botClient;

    public TelegramBotService(
        IOptions<TelegramSettings> settings,
        ICommandHandler commandHandler,
        IMessageHandler messageHandler,
        ILogger<TelegramBotService> logger,
        IVoiceMessageHandler? voiceMessageHandler = null)
    {
        _settings = settings.Value;
        _commandHandler = commandHandler;
        _messageHandler = messageHandler;
        _voiceMessageHandler = voiceMessageHandler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.BotToken))
        {
            _logger.LogError("Telegram bot token is not configured. Bot will not start.");
            return;
        }

        _botClient = new TelegramBotClient(_settings.BotToken);

        _logger.LogInformation("Starting Telegram bot with long-polling...");

        try
        {
            var me = await _botClient.GetMe(stoppingToken);
            _logger.LogInformation("Telegram bot @{Username} (ID: {BotId}) started successfully", me.Username, me.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Telegram API");
            return;
        }

        int? offset = null;
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message],
            DropPendingUpdates = true
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _botClient.GetUpdates(
                    offset,
                    timeout: 30,
                    allowedUpdates: receiverOptions.AllowedUpdates,
                    cancellationToken: stoppingToken);

                foreach (var update in updates)
                {
                    offset = update.Id + 1;

                    try
                    {
                        await ProcessUpdateAsync(_botClient, update, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing update {UpdateId}", update.Id);
                    }

                    if (stoppingToken.IsCancellationRequested)
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Telegram bot shutting down gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in long-polling loop. Retrying in 5 seconds...");
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

        _logger.LogInformation("Telegram bot stopped");
    }

    private async Task ProcessUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is null)
            return;

        // Voice message handling — check before text guard so voice notes are not silently dropped
        if (update.Message.Voice is not null)
        {
            if (_voiceMessageHandler is not null)
            {
                _logger.LogDebug("Received voice message from user {UserId} in chat {ChatId}",
                    update.Message.From?.Id, update.Message.Chat.Id);
                await _voiceMessageHandler.HandleVoiceMessageAsync(bot, update.Message, ct);
            }
            else
            {
                _logger.LogWarning("Received voice message but no voice handler is registered");
            }
            return;
        }

        if (update.Message is not { Text: not null } message)
            return;

        _logger.LogDebug("Received message from user {UserId} in chat {ChatId}",
            message.From?.Id, message.Chat.Id);

        // Call through the interface to use the instance method
        if (((ICommandHandler)_commandHandler).IsCommand(message.Text))
        {
            await _commandHandler.HandleAsync(bot, message, ct);
        }
        else
        {
            await _messageHandler.HandleAsync(bot, message, ct);
        }
    }
}
