using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Mimir.Api.Handlers.Voice.Messages;
using Mimir.Application.Telemetry;
using nem.Contracts.Voice;

namespace Mimir.Api.Handlers.Voice;

/// <summary>
/// Wolverine handler that orchestrates the complete voice pipeline:
/// STT (speech-to-text) → cognitive processing → TTS (text-to-speech).
/// Enforces 60s STT timeout and 30s TTS timeout from voice contracts.
/// </summary>
public sealed class VoicePipelineOrchestrator
{
    /// <summary>
    /// STT timeout in seconds (from <see cref="VoicePipelineOptions"/>).
    /// </summary>
    public const int SttTimeoutSeconds = 60;

    /// <summary>
    /// TTS timeout in seconds (from <see cref="VoicePipelineOptions"/>).
    /// </summary>
    public const int TtsTimeoutSeconds = 30;

    /// <summary>
    /// Handles a voice pipeline request by orchestrating STT → process → TTS.
    /// </summary>
    /// <param name="request">The pipeline request containing audio data.</param>
    /// <param name="sttProvider">Speech-to-text provider.</param>
    /// <param name="ttsProvider">Text-to-speech provider.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The pipeline result with transcription and synthesized audio.</returns>
    public static async Task<VoicePipelineResult> Handle(
        VoicePipelineRequest request,
        ISpeechToTextProvider sttProvider,
        ITextToSpeechProvider ttsProvider,
        ILogger<VoicePipelineOrchestrator> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.AudioData);

        using var activity = NemActivitySource.StartActivity("voice.pipeline");
        activity?.SetTag("voice.audio_length_bytes", request.AudioData.Length);
        activity?.SetTag("voice.channel_id", request.ChannelId);
        activity?.SetTag("voice.user_id", request.UserId);

        logger.LogInformation(
            "Starting voice pipeline for user {UserId} on channel {ChannelId}, audio: {AudioSize} bytes",
            request.UserId, request.ChannelId, request.AudioData.Length);

        // ── Step 1: STT with 60s timeout ───────────────────────────────────
        TranscriptionResult sttResult;
        try
        {
            using var sttTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            sttTimeoutCts.CancelAfter(TimeSpan.FromSeconds(SttTimeoutSeconds));

            using var audioStream = new MemoryStream(request.AudioData);
            sttResult = await sttProvider.TranscribeAsync(
                audioStream,
                request.LanguageHint,
                sttTimeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "Voice pipeline STT timed out after {Timeout}s for user {UserId}",
                SttTimeoutSeconds, request.UserId);

            activity?.SetTag("voice.pipeline_stage", "stt");
            activity?.SetTag("voice.success", false);
            activity?.SetStatus(ActivityStatusCode.Error, "STT timeout");

            return new VoicePipelineResult(
                TranscribedText: null,
                ResponseText: null,
                ResponseAudio: null,
                Success: false,
                ErrorMessage: $"STT stage timed out after {SttTimeoutSeconds} seconds.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Voice pipeline STT failed for user {UserId}: {Error}",
                request.UserId, ex.Message);

            activity?.SetTag("voice.pipeline_stage", "stt");
            activity?.SetTag("voice.success", false);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return new VoicePipelineResult(
                TranscribedText: null,
                ResponseText: null,
                ResponseAudio: null,
                Success: false,
                ErrorMessage: $"STT stage failed: {ex.Message}");
        }

        var transcribedText = sttResult.Text;
        activity?.SetTag("voice.transcribed_text_length", transcribedText.Length);

        logger.LogInformation(
            "Voice pipeline STT complete for user {UserId}: '{TranscribedText}'",
            request.UserId,
            transcribedText.Length > 100 ? transcribedText[..100] + "..." : transcribedText);

        // ── Step 2: Cognitive processing (echo-back for V1) ────────────────
        // In a full pipeline, this would dispatch to the cognitive message pipeline.
        // For Voice V1, we return the transcribed text as the response text.
        var responseText = transcribedText;
        activity?.SetTag("voice.response_text_length", responseText.Length);

        // ── Step 3: TTS with 30s timeout ───────────────────────────────────
        byte[] audioData;
        try
        {
            using var ttsTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ttsTimeoutCts.CancelAfter(TimeSpan.FromSeconds(TtsTimeoutSeconds));

            var chunks = new List<byte[]>();
            await foreach (var chunk in ttsProvider.SynthesizeAsync(
                responseText,
                request.VoiceId,
                ttsTimeoutCts.Token))
            {
                chunks.Add(chunk);
            }

            audioData = CombineChunks(chunks);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "Voice pipeline TTS timed out after {Timeout}s for user {UserId}",
                TtsTimeoutSeconds, request.UserId);

            activity?.SetTag("voice.pipeline_stage", "tts");
            activity?.SetTag("voice.success", false);
            activity?.SetStatus(ActivityStatusCode.Error, "TTS timeout");

            return new VoicePipelineResult(
                TranscribedText: transcribedText,
                ResponseText: responseText,
                ResponseAudio: null,
                Success: false,
                ErrorMessage: $"TTS stage timed out after {TtsTimeoutSeconds} seconds.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Voice pipeline TTS failed for user {UserId}: {Error}",
                request.UserId, ex.Message);

            activity?.SetTag("voice.pipeline_stage", "tts");
            activity?.SetTag("voice.success", false);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return new VoicePipelineResult(
                TranscribedText: transcribedText,
                ResponseText: responseText,
                ResponseAudio: null,
                Success: false,
                ErrorMessage: $"TTS stage failed: {ex.Message}");
        }

        activity?.SetTag("voice.success", true);
        activity?.SetTag("voice.audio_response_length_bytes", audioData.Length);

        logger.LogInformation(
            "Voice pipeline complete for user {UserId}: STT {SttLength} chars → TTS {TtsSize} bytes",
            request.UserId, transcribedText.Length, audioData.Length);

        return new VoicePipelineResult(
            TranscribedText: transcribedText,
            ResponseText: responseText,
            ResponseAudio: audioData,
            Success: true);
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
