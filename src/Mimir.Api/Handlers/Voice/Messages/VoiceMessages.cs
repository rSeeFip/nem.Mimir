namespace Mimir.Api.Handlers.Voice.Messages;

/// <summary>
/// Inbound voice message received from a channel (e.g., Telegram voice note).
/// Contains raw audio bytes for STT processing.
/// </summary>
/// <param name="AudioData">Raw audio bytes (WAV format expected).</param>
/// <param name="ChannelId">External channel identifier.</param>
/// <param name="UserId">External user identifier.</param>
/// <param name="LanguageHint">Optional language hint for STT (e.g., "en", "es").</param>
/// <param name="MimeType">Audio MIME type (e.g., "audio/wav", "audio/ogg").</param>
/// <param name="DurationSeconds">Duration of the audio in seconds, if known.</param>
public sealed record VoiceMessageReceived(
    byte[] AudioData,
    string ChannelId,
    string UserId,
    string? LanguageHint = null,
    string? MimeType = null,
    double? DurationSeconds = null);

/// <summary>
/// Result of processing a voice message through STT.
/// </summary>
/// <param name="TranscribedText">The text transcription from STT.</param>
/// <param name="Confidence">STT confidence score (0.0–1.0).</param>
/// <param name="Language">Detected or provided language code.</param>
/// <param name="Duration">Duration of the transcribed audio.</param>
/// <param name="Success">Whether transcription succeeded.</param>
/// <param name="ErrorMessage">Error message if transcription failed.</param>
public sealed record VoiceMessageResult(
    string? TranscribedText,
    double Confidence,
    string? Language,
    TimeSpan Duration,
    bool Success,
    string? ErrorMessage = null);

/// <summary>
/// Request to synthesize text into voice audio via TTS.
/// </summary>
/// <param name="Text">The text to synthesize into speech.</param>
/// <param name="ChannelId">Target channel identifier for sending the audio.</param>
/// <param name="UserId">Target user identifier.</param>
/// <param name="VoiceId">Optional voice identifier for voice selection.</param>
public sealed record VoiceSynthesisRequest(
    string Text,
    string ChannelId,
    string UserId,
    string? VoiceId = null);

/// <summary>
/// Result of TTS synthesis.
/// </summary>
/// <param name="AudioData">Synthesized audio bytes (WAV format).</param>
/// <param name="MimeType">Audio MIME type.</param>
/// <param name="DurationSeconds">Duration of the synthesized audio.</param>
/// <param name="Success">Whether synthesis succeeded.</param>
/// <param name="ErrorMessage">Error message if synthesis failed.</param>
public sealed record VoiceSynthesisResult(
    byte[]? AudioData,
    string MimeType,
    double? DurationSeconds,
    bool Success,
    string? ErrorMessage = null);

/// <summary>
/// Request to process a complete voice pipeline: STT → cognitive processing → TTS.
/// </summary>
/// <param name="AudioData">Raw audio bytes for STT.</param>
/// <param name="ChannelId">Channel identifier for the conversation.</param>
/// <param name="UserId">User identifier.</param>
/// <param name="LanguageHint">Optional language hint for STT.</param>
/// <param name="VoiceId">Optional voice identifier for TTS output.</param>
public sealed record VoicePipelineRequest(
    byte[] AudioData,
    string ChannelId,
    string UserId,
    string? LanguageHint = null,
    string? VoiceId = null);

/// <summary>
/// Result of the complete voice pipeline.
/// </summary>
/// <param name="TranscribedText">Text transcription from STT.</param>
/// <param name="ResponseText">Cognitive response text.</param>
/// <param name="ResponseAudio">Synthesized audio of the response.</param>
/// <param name="Success">Whether the entire pipeline succeeded.</param>
/// <param name="ErrorMessage">Error message if pipeline failed.</param>
public sealed record VoicePipelineResult(
    string? TranscribedText,
    string? ResponseText,
    byte[]? ResponseAudio,
    bool Success,
    string? ErrorMessage = null);
