namespace nem.Mimir.Voice;

/// <summary>
/// Abstraction for a text-to-speech engine backend.
/// Allows mocking the actual TTS binary (e.g., piper-tts) in tests.
/// </summary>
public interface ITtsBackend
{
    /// <summary>
    /// Generates raw PCM audio bytes from the given text using the specified voice model.
    /// The output is expected to be 16-bit PCM at the configured sample rate.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="voiceModel">The voice model identifier or path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Raw audio bytes (WAV format including header).</returns>
    Task<byte[]> GenerateAudioAsync(string text, string voiceModel, CancellationToken ct);
}
