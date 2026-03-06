namespace Mimir.Voice.Configuration;

/// <summary>
/// Configuration options for the Whisper speech-to-text provider.
/// </summary>
public sealed class WhisperOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Whisper";

    /// <summary>
    /// Path to the Whisper GGML model file.
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Language hint for transcription (e.g., "en", "es").
    /// Null for auto-detection.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Maximum allowed audio duration in seconds.
    /// </summary>
    public int MaxDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Timeout for transcription processing in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
