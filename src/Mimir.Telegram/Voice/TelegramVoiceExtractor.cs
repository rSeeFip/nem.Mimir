using System.Diagnostics;
using Telegram.Bot;
using TelegramVoice = Telegram.Bot.Types.Voice;

namespace Mimir.Telegram.Voice;

/// <summary>
/// Extracts audio from Telegram voice notes, converting OGG to WAV format.
/// </summary>
public interface ITelegramVoiceExtractor
{
    /// <summary>
    /// Downloads and converts a Telegram voice note to WAV format.
    /// </summary>
    /// <param name="botClient">The Telegram bot client for downloading files.</param>
    /// <param name="voice">The voice note from the Telegram message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>WAV audio bytes (16kHz, mono, PCM16).</returns>
    Task<byte[]> ExtractVoiceAudioAsync(ITelegramBotClient botClient, TelegramVoice voice, CancellationToken ct = default);
}

/// <summary>
/// Downloads Telegram voice notes and converts from OGG/Opus to 16kHz mono PCM16 WAV.
/// </summary>
internal sealed class TelegramVoiceExtractor : ITelegramVoiceExtractor
{
    private readonly IAudioFormatConverter _converter;
    private readonly ILogger<TelegramVoiceExtractor> _logger;

    private static readonly ActivitySource s_activitySource = new("nem.Mimir.Telegram.Voice", "1.0.0");

    public TelegramVoiceExtractor(IAudioFormatConverter converter, ILogger<TelegramVoiceExtractor> logger)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<byte[]> ExtractVoiceAudioAsync(ITelegramBotClient botClient, TelegramVoice voice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(botClient);
        ArgumentNullException.ThrowIfNull(voice);

        using var activity = s_activitySource.StartActivity("voice.extract");
        activity?.SetTag("voice.file_id", voice.FileId);
        activity?.SetTag("voice.duration_seconds", voice.Duration);
        activity?.SetTag("voice.mime_type", voice.MimeType ?? "audio/ogg");

        if (voice.Duration > AudioFormatConverter.MaxDurationSeconds)
        {
            _logger.LogWarning("Voice note exceeds {MaxDuration}s limit: {Duration}s",
                AudioFormatConverter.MaxDurationSeconds, voice.Duration);
        }

        _logger.LogInformation("Downloading voice note {FileId}, duration: {Duration}s",
            voice.FileId, voice.Duration);

        // Step 1: Get file info from Telegram
        var file = await botClient.GetFile(voice.FileId, ct);

        if (string.IsNullOrEmpty(file.FilePath))
        {
            throw new InvalidOperationException($"Telegram returned empty file path for voice note {voice.FileId}");
        }

        // Step 2: Download the OGG file
        using var downloadStream = new MemoryStream();
        await botClient.DownloadFile(file.FilePath, downloadStream, ct);
        var oggData = downloadStream.ToArray();

        activity?.SetTag("voice.downloaded_size_bytes", oggData.Length);

        _logger.LogDebug("Downloaded voice note {FileId}: {Size} bytes",
            voice.FileId, oggData.Length);

        if (oggData.Length == 0)
        {
            throw new InvalidOperationException($"Downloaded empty voice note for file {voice.FileId}");
        }

        // Step 3: Convert OGG to WAV (16kHz mono PCM16)
        var wavData = await _converter.ConvertOggToWavAsync(oggData, ct);

        activity?.SetTag("voice.wav_size_bytes", wavData.Length);
        activity?.SetTag("voice.success", true);

        _logger.LogInformation("Converted voice note {FileId} to WAV: {WavSize} bytes",
            voice.FileId, wavData.Length);

        return wavData;
    }
}
