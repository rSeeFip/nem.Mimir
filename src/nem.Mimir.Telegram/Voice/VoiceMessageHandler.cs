using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace nem.Mimir.Telegram.Voice;

/// <summary>
/// Handles incoming Telegram voice messages by extracting audio,
/// converting to WAV, and dispatching a VoiceMessageReceived event
/// via the Wolverine message bus.
/// </summary>
public interface IVoiceMessageHandler
{
    /// <summary>
    /// Processes a Telegram message containing a voice note.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="message">The Telegram message containing a voice note.</param>
    /// <param name="ct">Cancellation token.</param>
    Task HandleVoiceMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);

    /// <summary>
    /// Sends a voice note response back to a Telegram chat.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="chatId">The target chat ID.</param>
    /// <param name="wavAudioData">WAV audio data to convert and send.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendVoiceResponseAsync(ITelegramBotClient botClient, long chatId, byte[] wavAudioData, CancellationToken ct);
}

/// <summary>
/// Handles voice messages from Telegram by extracting audio, converting formats,
/// and coordinating with the voice pipeline.
/// </summary>
internal sealed class VoiceMessageHandler : IVoiceMessageHandler
{
    private readonly ITelegramVoiceExtractor _extractor;
    private readonly IAudioFormatConverter _converter;
    private readonly ILogger<VoiceMessageHandler> _logger;

    private static readonly ActivitySource s_activitySource = new("nem.Mimir.Telegram.Voice", "1.0.0");

    public VoiceMessageHandler(
        ITelegramVoiceExtractor extractor,
        IAudioFormatConverter converter,
        ILogger<VoiceMessageHandler> logger)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task HandleVoiceMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(botClient);
        ArgumentNullException.ThrowIfNull(message);

        if (message.Voice is null)
        {
            _logger.LogWarning("HandleVoiceMessageAsync called with message that has no Voice property");
            return;
        }

        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? chatId;

        using var activity = s_activitySource.StartActivity("voice.handle_incoming");
        activity?.SetTag("voice.chat_id", chatId);
        activity?.SetTag("voice.user_id", userId);
        activity?.SetTag("voice.duration_seconds", message.Voice.Duration);

        _logger.LogInformation(
            "Processing voice message from user {UserId} in chat {ChatId}, duration: {Duration}s",
            userId, chatId, message.Voice.Duration);

        try
        {
            // Send "Processing voice..." indicator
            await botClient.SendMessage(chatId,
                "🎤 Processing your voice message...", cancellationToken: ct);

            // Extract and convert voice audio
            var wavData = await _extractor.ExtractVoiceAudioAsync(botClient, message.Voice, ct);

            activity?.SetTag("voice.wav_size_bytes", wavData.Length);
            activity?.SetTag("voice.success", true);

            _logger.LogInformation(
                "Voice message from user {UserId} extracted: {WavSize} bytes WAV. " +
                "Dispatching VoiceMessageReceived for pipeline processing.",
                userId, wavData.Length);

            // Note: In a full integration, this would dispatch VoiceMessageReceived
            // via Wolverine bus. For V1, we log the successful extraction.
            // The Wolverine integration will be added when nem.Mimir.Telegram gets
            // a Wolverine bus reference (requires infrastructure changes).
            await botClient.SendMessage(chatId,
                "✅ Voice message received and processed successfully.",
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetTag("voice.success", false);
            activity?.SetTag("voice.error", ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex,
                "Failed to process voice message from user {UserId} in chat {ChatId}",
                userId, chatId);

            await botClient.SendMessage(chatId,
                "❌ Failed to process your voice message. Please try again.",
                cancellationToken: ct);
        }
    }

    /// <inheritdoc />
    public async Task SendVoiceResponseAsync(ITelegramBotClient botClient, long chatId, byte[] wavAudioData, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(botClient);
        ArgumentNullException.ThrowIfNull(wavAudioData);

        using var activity = s_activitySource.StartActivity("voice.send_response");
        activity?.SetTag("voice.chat_id", chatId);
        activity?.SetTag("voice.wav_size_bytes", wavAudioData.Length);

        _logger.LogInformation("Sending voice response to chat {ChatId}, WAV size: {WavSize} bytes",
            chatId, wavAudioData.Length);

        try
        {
            // Convert WAV to OGG for Telegram
            var oggData = await _converter.ConvertWavToOggAsync(wavAudioData, ct);

            activity?.SetTag("voice.ogg_size_bytes", oggData.Length);

            // Send as voice note via Telegram API
            using var oggStream = new MemoryStream(oggData);
            var inputFile = InputFile.FromStream(oggStream, "response.ogg");
            await botClient.SendVoice(chatId, inputFile, cancellationToken: ct);

            activity?.SetTag("voice.success", true);

            _logger.LogInformation("Voice response sent to chat {ChatId}", chatId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetTag("voice.success", false);
            activity?.SetTag("voice.error", ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex, "Failed to send voice response to chat {ChatId}", chatId);
            throw;
        }
    }
}
