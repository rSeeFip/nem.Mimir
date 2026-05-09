namespace nem.Mimir.Voice;

/// <summary>
/// Validates and preprocesses audio streams for Whisper transcription.
/// Ensures audio is 16kHz mono PCM16 WAV format within duration limits.
/// </summary>
public class AudioPreprocessor
{
    private const int ExpectedSampleRate = 16000;
    private const int ExpectedChannels = 1;
    private const int ExpectedBitsPerSample = 16;

    /// <summary>
    /// Validates a WAV audio stream and extracts PCM float samples for Whisper processing.
    /// </summary>
    /// <param name="audioStream">The audio stream to validate and preprocess.</param>
    /// <param name="maxDurationSeconds">Maximum allowed duration in seconds.</param>
    /// <returns>Audio validation result containing float samples if valid.</returns>
    public AudioPreprocessResult Process(Stream audioStream, int maxDurationSeconds)
    {
        ArgumentNullException.ThrowIfNull(audioStream);

        if (audioStream.Length == 0 || !audioStream.CanRead)
        {
            return AudioPreprocessResult.Failure("Audio stream is empty or unreadable.");
        }

        // Read WAV header
        using var reader = new BinaryReader(audioStream, System.Text.Encoding.UTF8, leaveOpen: true);

        // RIFF header
        if (audioStream.Length < 44)
        {
            return AudioPreprocessResult.Failure("Invalid WAV file: too short for WAV header.");
        }

        var riffId = new string(reader.ReadChars(4));
        if (riffId != "RIFF")
        {
            return AudioPreprocessResult.Failure("Invalid WAV file: missing RIFF header.");
        }

        _ = reader.ReadInt32(); // file size
        var waveId = new string(reader.ReadChars(4));
        if (waveId != "WAVE")
        {
            return AudioPreprocessResult.Failure("Invalid WAV file: missing WAVE identifier.");
        }

        // fmt chunk
        var fmtId = new string(reader.ReadChars(4));
        if (fmtId != "fmt ")
        {
            return AudioPreprocessResult.Failure("Invalid WAV file: missing fmt chunk.");
        }

        var fmtSize = reader.ReadInt32();
        var audioFormat = reader.ReadInt16();
        var channels = reader.ReadInt16();
        var sampleRate = reader.ReadInt32();
        _ = reader.ReadInt32(); // byte rate
        _ = reader.ReadInt16(); // block align
        var bitsPerSample = reader.ReadInt16();

        // Skip extra fmt bytes if present
        if (fmtSize > 16)
        {
            reader.ReadBytes(fmtSize - 16);
        }

        // Validate PCM format
        if (audioFormat != 1)
        {
            return AudioPreprocessResult.Failure($"Unsupported audio format: expected PCM (1), got {audioFormat}.");
        }

        if (sampleRate != ExpectedSampleRate)
        {
            return AudioPreprocessResult.Failure($"Unsupported sample rate: expected {ExpectedSampleRate}Hz, got {sampleRate}Hz.");
        }

        if (channels != ExpectedChannels)
        {
            return AudioPreprocessResult.Failure($"Unsupported channel count: expected {ExpectedChannels} (mono), got {channels}.");
        }

        if (bitsPerSample != ExpectedBitsPerSample)
        {
            return AudioPreprocessResult.Failure($"Unsupported bits per sample: expected {ExpectedBitsPerSample}, got {bitsPerSample}.");
        }

        // Find data chunk
        string chunkId;
        int chunkSize;
        while (true)
        {
            if (audioStream.Position + 8 > audioStream.Length)
            {
                return AudioPreprocessResult.Failure("Invalid WAV file: data chunk not found.");
            }

            chunkId = new string(reader.ReadChars(4));
            chunkSize = reader.ReadInt32();

            if (chunkId == "data")
                break;

            // Skip non-data chunks
            if (audioStream.Position + chunkSize > audioStream.Length)
            {
                return AudioPreprocessResult.Failure("Invalid WAV file: truncated chunk.");
            }
            reader.ReadBytes(chunkSize);
        }

        // Calculate duration
        var bytesPerSample = bitsPerSample / 8;
        var totalSamples = chunkSize / (bytesPerSample * channels);
        var durationSeconds = (double)totalSamples / sampleRate;

        if (durationSeconds > maxDurationSeconds)
        {
            return AudioPreprocessResult.Failure(
                $"Audio duration ({durationSeconds:F1}s) exceeds maximum allowed ({maxDurationSeconds}s).");
        }

        // Read PCM data and convert to float samples
        var pcmData = reader.ReadBytes(chunkSize);
        var samples = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            var sample = BitConverter.ToInt16(pcmData, i * 2);
            samples[i] = sample / 32768f;
        }

        return AudioPreprocessResult.Success(samples, TimeSpan.FromSeconds(durationSeconds), sampleRate, channels);
    }
}

/// <summary>
/// Result of audio preprocessing.
/// </summary>
public sealed class AudioPreprocessResult
{
    /// <summary>
    /// Whether the audio was successfully preprocessed.
    /// </summary>
    public bool IsValid { get; private init; }

    /// <summary>
    /// Error message if preprocessing failed.
    /// </summary>
    public string? Error { get; private init; }

    /// <summary>
    /// PCM float samples extracted from the audio.
    /// </summary>
    public float[]? Samples { get; private init; }

    /// <summary>
    /// Duration of the audio.
    /// </summary>
    public TimeSpan Duration { get; private init; }

    /// <summary>
    /// Sample rate of the audio.
    /// </summary>
    public int SampleRate { get; private init; }

    /// <summary>
    /// Number of channels.
    /// </summary>
    public int Channels { get; private init; }

    /// <summary>
    /// Creates a successful preprocessing result.
    /// </summary>
    public static AudioPreprocessResult Success(float[] samples, TimeSpan duration, int sampleRate, int channels)
        => new()
        {
            IsValid = true,
            Samples = samples,
            Duration = duration,
            SampleRate = sampleRate,
            Channels = channels
        };

    /// <summary>
    /// Creates a failed preprocessing result.
    /// </summary>
    public static AudioPreprocessResult Failure(string error)
        => new()
        {
            IsValid = false,
            Error = error
        };
}
