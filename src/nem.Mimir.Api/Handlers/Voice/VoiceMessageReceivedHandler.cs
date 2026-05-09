using System.Diagnostics;
using Microsoft.Extensions.Logging;
using nem.Mimir.Api.Handlers.Voice.Messages;
using nem.Mimir.Application.Telemetry;
using nem.Contracts.Voice;

namespace nem.Mimir.Api.Handlers.Voice;

/// <summary>
/// Wolverine handler for inbound voice messages. Performs STT transcription
/// and produces a <see cref="VoiceMessageResult"/> with the transcribed text.
/// </summary>
public sealed class VoiceMessageReceivedHandler
{
    /// <summary>
    /// Handles an inbound voice message by transcribing the audio via STT.
    /// </summary>
    /// <param name="message">The inbound voice message containing audio data.</param>
    /// <param name="sttProvider">Speech-to-text provider.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transcription result.</returns>
    public static async Task<VoiceMessageResult> Handle(
        VoiceMessageReceived message,
        ISpeechToTextProvider sttProvider,
        ILogger<VoiceMessageReceivedHandler> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.AudioData);

        using var activity = NemActivitySource.StartActivity("voice.stt");
        activity?.SetTag("voice.audio_length_bytes", message.AudioData.Length);
        activity?.SetTag("voice.language_hint", message.LanguageHint ?? "auto");
        activity?.SetTag("voice.channel_id", message.ChannelId);
        activity?.SetTag("voice.user_id", message.UserId);

        if (message.AudioData.Length == 0)
        {
            logger.LogWarning("Received empty audio data from user {UserId} on channel {ChannelId}",
                message.UserId, message.ChannelId);

            activity?.SetTag("voice.success", false);
            activity?.SetTag("voice.error", "empty_audio");

            return new VoiceMessageResult(
                TranscribedText: null,
                Confidence: 0,
                Language: null,
                Duration: TimeSpan.Zero,
                Success: false,
                ErrorMessage: "Audio data is empty.");
        }

        // Enforce 60s STT timeout from contracts
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            using var audioStream = new MemoryStream(message.AudioData);

            logger.LogInformation(
                "Starting STT transcription for user {UserId} on channel {ChannelId}, audio size: {AudioSize} bytes",
                message.UserId, message.ChannelId, message.AudioData.Length);

            var result = await sttProvider.TranscribeAsync(
                audioStream,
                message.LanguageHint,
                timeoutCts.Token);

            activity?.SetTag("voice.success", true);
            activity?.SetTag("voice.transcription_length", result.Text.Length);
            activity?.SetTag("voice.confidence", result.Confidence);
            activity?.SetTag("voice.detected_language", result.Language);
            activity?.SetTag("voice.duration_seconds", result.Duration.TotalSeconds);

            logger.LogInformation(
                "STT transcription complete for user {UserId}: {TextLength} chars, confidence {Confidence:F2}, language {Language}",
                message.UserId, result.Text.Length, result.Confidence, result.Language ?? "unknown");

            return new VoiceMessageResult(
                TranscribedText: result.Text,
                Confidence: result.Confidence,
                Language: result.Language,
                Duration: result.Duration,
                Success: true);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "STT transcription timed out after 60s for user {UserId} on channel {ChannelId}",
                message.UserId, message.ChannelId);

            activity?.SetTag("voice.success", false);
            activity?.SetTag("voice.error", "timeout");
            activity?.SetStatus(ActivityStatusCode.Error, "STT timeout");

            return new VoiceMessageResult(
                TranscribedText: null,
                Confidence: 0,
                Language: null,
                Duration: TimeSpan.Zero,
                Success: false,
                ErrorMessage: "STT transcription timed out after 60 seconds.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "STT transcription failed for user {UserId} on channel {ChannelId}: {Error}",
                message.UserId, message.ChannelId, ex.Message);

            activity?.SetTag("voice.success", false);
            activity?.SetTag("voice.error", ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return new VoiceMessageResult(
                TranscribedText: null,
                Confidence: 0,
                Language: null,
                Duration: TimeSpan.Zero,
                Success: false,
                ErrorMessage: $"STT transcription failed: {ex.Message}");
        }
    }
}
