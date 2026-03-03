using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Mimir.Telegram.Configuration;
using Mimir.Telegram.Services;

namespace Mimir.Telegram.Handlers;

/// <summary>
/// Handles regular (non-command) text messages by sending them to the Mimir API
/// and streaming the response back to the Telegram user.
/// </summary>
internal sealed class MessageHandler : IMessageHandler
{
    private readonly MimirApiClient _apiClient;
    private readonly UserStateManager _stateManager;
    private readonly TelegramSettings _settings;
    private readonly ILogger<MessageHandler> _logger;

    public MessageHandler(
        MimirApiClient apiClient,
        UserStateManager stateManager,
        IOptions<TelegramSettings> settings,
        ILogger<MessageHandler> logger)
    {
        _apiClient = apiClient;
        _stateManager = stateManager;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes a text message from a Telegram user.
    /// </summary>
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

        _apiClient.SetBearerToken(state.BearerToken);

        _logger.LogDebug("Sending message from user {UserId} to conversation {ConversationId}",
            userId, state.CurrentConversationId);

        // Send initial "Thinking..." message
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

            // Final update with complete response
            var finalResponse = responseBuilder.Length > 0
                ? responseBuilder.ToString()
                : "⚠️ The AI returned an empty response.";

            await TryEditMessageAsync(bot, chatId, thinkingMessage.Id, finalResponse, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown — update with whatever we have
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
            // Telegram has a 4096 character limit for messages
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
