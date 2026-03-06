using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Mimir.Api.Handlers.Voice.Messages;
using Mimir.Application.Telemetry;
using nem.Contracts.Voice;

namespace Mimir.Api.Handlers.Voice;

/// <summary>
/// Wolverine handler for TTS synthesis requests. Converts text to audio
/// via <see cref="ITextToSpeechProvider"/> and returns synthesized voice content.
/// </summary>
public sealed class VoiceSynthesisRequestHandler
{
    /// <summary>
    /// Handles a voice synthesis request by converting text to audio via TTS.
    /// </summary>
    /// <param name="request">The synthesis request containing text to convert.</param>
    /// <param name="ttsProvider">Text-to-speech provider.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The synthesis result with audio data.</returns>
    public static async Task<VoiceSynthesisResult> Handle(
        VoiceSynthesisRequest request,
        ITextToSpeechProvider ttsProvider,
        ILogger<VoiceSynthesisRequestHandler> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Text);

        using var activity = NemActivitySource.StartActivity("voice.tts");
        activity?.SetTag("voice.text_length", request.Text.Length);
        activity?.SetTag("voice.channel_id", request.ChannelId);
        activity?.SetTag("voice.user_id", request.UserId);
        activity?.SetTag("voice.voice_id", request.VoiceId ?? "default");

        // Enforce 30s TTS timeout from contracts
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            logger.LogInformation(
                "Starting TTS synthesis for user {UserId} on channel {ChannelId}, text length: {TextLength}",
                request.UserId, request.ChannelId, request.Text.Length);

            // Collect all audio chunks into a single byte array
            var chunks = new List<byte[]>();
            await foreach (var chunk in ttsProvider.SynthesizeAsync(
                request.Text,
                request.VoiceId,
                timeoutCts.Token))
            {
                chunks.Add(chunk);
            }

            var audioData = CombineChunks(chunks);

            // Estimate duration: 16kHz mono 16-bit PCM = 32000 bytes/second
            // WAV header is 44 bytes
            const int bytesPerSecond = 32000;
            var estimatedDuration = audioData.Length > 44
                ? (double)(audioData.Length - 44) / bytesPerSecond
                : 0;

            activity?.SetTag("voice.success", true);
            activity?.SetTag("voice.audio_length_bytes", audioData.Length);
            activity?.SetTag("voice.duration_seconds", estimatedDuration);

            logger.LogInformation(
                "TTS synthesis complete for user {UserId}: {AudioSize} bytes, ~{Duration:F1}s",
                request.UserId, audioData.Length, estimatedDuration);

            return new VoiceSynthesisResult(
                AudioData: audioData,
                MimeType: "audio/wav",
                DurationSeconds: estimatedDuration,
                Success: true);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "TTS synthesis timed out after 30s for user {UserId} on channel {ChannelId}",
                request.UserId, request.ChannelId);

            activity?.SetTag("voice.success", false);
            activity?.SetTag("voice.error", "timeout");
            activity?.SetStatus(ActivityStatusCode.Error, "TTS timeout");

            return new VoiceSynthesisResult(
                AudioData: null,
                MimeType: "audio/wav",
                DurationSeconds: null,
                Success: false,
                ErrorMessage: "TTS synthesis timed out after 30 seconds.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "TTS synthesis failed for user {UserId} on channel {ChannelId}: {Error}",
                request.UserId, request.ChannelId, ex.Message);

            activity?.SetTag("voice.success", false);
            activity?.SetTag("voice.error", ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return new VoiceSynthesisResult(
                AudioData: null,
                MimeType: "audio/wav",
                DurationSeconds: null,
                Success: false,
                ErrorMessage: $"TTS synthesis failed: {ex.Message}");
        }
    }

    private static byte[] CombineChunks(List<byte[]> chunks)
    {
        var totalLength = 0;
        foreach (var chunk in chunks)
        {
            totalLength += chunk.Length;
        }

        var result = new byte[totalLength];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        return result;
    }
}
