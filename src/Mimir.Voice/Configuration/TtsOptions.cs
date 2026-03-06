namespace Mimir.Voice.Configuration;

/// <summary>
/// Configuration options for the text-to-speech provider.
/// </summary>
public sealed class TtsOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Tts";

    /// <summary>
    /// Path to the piper-tts model file (.onnx).
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Default voice identifier used when none is specified.
    /// </summary>
    public string DefaultVoiceId { get; set; } = "en_US-lessac-medium";

    /// <summary>
    /// Maximum number of characters allowed for synthesis.
    /// Text exceeding this limit will be truncated with a warning.
    /// </summary>
    public int MaxCharacters { get; set; } = 1000;

    /// <summary>
    /// Audio sample rate in Hz. Default: 16000 (16 kHz).
    /// </summary>
    public int SampleRate { get; set; } = 16000;
}
