using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using nem.Mimir.Application.Common.Sanitization;
using nem.Mimir.Telegram.Configuration;
using nem.Mimir.Telegram.Services;

namespace nem.Mimir.Telegram.Handlers;

internal sealed class MessageHandler : IMessageHandler
{
    private const string ChannelName = "telegram";

    private readonly MimirApiClient _apiClient;
    private readonly UserStateManager _stateManager;
    private readonly TelegramSettings _settings;
    private readonly ILogger<MessageHandler> _logger;
    private readonly ISanitizationService _sanitizationService;
    private readonly SanitizationOptions _sanitizationOptions;

    public MessageHandler(
        MimirApiClient apiClient,
        UserStateManager stateManager,
        IOptions<TelegramSettings> settings,
        ILogger<MessageHandler> logger,
        ISanitizationService sanitizationService,
        IOptions<SanitizationOptions> sanitizationOptions)
    {
        _apiClient = apiClient;
        _stateManager = stateManager;
        _settings = settings.Value;
        _logger = logger;
        _sanitizationService = sanitizationService;
        _sanitizationOptions = sanitizationOptions.Value;
    }

    public async Task HandleAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? chatId;
        var text = message.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return;

        var state = _stateManager.GetState(userId);
        if (state is not { IsAuthenticated: true, BearerToken: not null })
        {
            await bot.SendMessage(chatId, "🔒 Please authenticate first using /start", cancellationToken: ct);
            return;
        }

        if (state.CurrentConversationId is null)
        {
            await bot.SendMessage(chatId,
                "📝 No active conversation. Use /new to create one, or /list to see existing ones.",
                cancellationToken: ct);
            return;
        }

        var mode = _sanitizationOptions.EnforcementEnabled
            ? _sanitizationOptions.GetModeForChannel(ChannelName)
            : SanitizationMode.Log;

        if (_sanitizationService.ContainsSuspiciousPatterns(text))
        {
            _logger.LogWarning("Suspicious patterns detected in telegram message from user {UserId}", userId);

            if (mode == SanitizationMode.Block)
            {
                await bot.SendMessage(chatId,
                    "⚠️ Your message was blocked due to suspicious content. Please rephrase and try again.",
                    cancellationToken: ct);
                return;
            }
        }

        if (mode != SanitizationMode.Log)
        {
            text = _sanitizationService.SanitizeUserInput(text);
        }

        _apiClient.SetBearerToken(state.BearerToken);

        _logger.LogDebug("Sending message from user {UserId} to conversation {ConversationId}",
            userId, state.CurrentConversationId);

        var thinkingMessage = await bot.SendMessage(chatId, "🤔 Thinking...", cancellationToken: ct);
        var responseBuilder = new StringBuilder();
        var lastUpdateTime = DateTimeOffset.UtcNow;
        var updateInterval = TimeSpan.FromMilliseconds(_settings.StreamingUpdateIntervalMs);

        try
        {
            await foreach (var token in _apiClient.SendMessageStreamingAsync(
                state.CurrentConversationId.Value, text, state.SelectedModel, ct))
            {
                responseBuilder.Append(token);

                var now = DateTimeOffset.UtcNow;
                if (now - lastUpdateTime >= updateInterval)
                {
                    await TryEditMessageAsync(bot, chatId, thinkingMessage.Id,
                        responseBuilder.ToString(), ct);
                    lastUpdateTime = now;
                }
            }

            var finalResponse = responseBuilder.Length > 0
                ? responseBuilder.ToString()
                : "⚠️ The AI returned an empty response.";

            await TryEditMessageAsync(bot, chatId, thinkingMessage.Id, finalResponse, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (responseBuilder.Length > 0)
            {
                await TryEditMessageAsync(bot, chatId, thinkingMessage.Id,
                    responseBuilder + "\n\n⚠️ _Response interrupted._", CancellationToken.None);
            }
        }
        catch (Exception ex) // Intentional catch-all: Telegram message processing errors must not crash the handler; user gets error message
        {
            _logger.LogError(ex, "Error processing message for user {UserId}", userId);
            await TryEditMessageAsync(bot, chatId, thinkingMessage.Id,
                "❌ An error occurred while processing your message. Please try again.", ct);
        }
    }

    private async Task TryEditMessageAsync(ITelegramBotClient bot, long chatId, int messageId, string text, CancellationToken ct)
    {
        try
        {
            if (text.Length > 4096)
            {
                text = text[..4093] + "...";
            }

            await bot.EditMessageText(chatId, messageId, text, cancellationToken: ct);
        }
        catch (Exception ex) // Intentional catch-all: Telegram API edit failures are non-critical; message may have been deleted
        {
            _logger.LogDebug(ex, "Failed to edit message {MessageId} in chat {ChatId}", messageId, chatId);
        }
    }
}
