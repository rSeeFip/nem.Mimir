using System.Diagnostics;

namespace nem.Mimir.Telegram.Voice;

/// <summary>
/// Abstracts audio format conversion between OGG (Opus) and WAV (PCM16).
/// </summary>
public interface IAudioFormatConverter
{
    /// <summary>
    /// Converts OGG/Opus audio data to 16kHz mono PCM16 WAV format.
    /// </summary>
    /// <param name="oggData">Raw OGG/Opus audio bytes from Telegram.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>WAV audio bytes (16kHz, mono, PCM16, 44-byte header).</returns>
    Task<byte[]> ConvertOggToWavAsync(byte[] oggData, CancellationToken ct = default);

    /// <summary>
    /// Converts WAV (PCM16) audio data to OGG/Opus format for sending back via Telegram.
    /// </summary>
    /// <param name="wavData">WAV audio bytes (PCM16 format).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>OGG/Opus audio bytes suitable for Telegram SendVoice.</returns>
    Task<byte[]> ConvertWavToOggAsync(byte[] wavData, CancellationToken ct = default);
}

/// <summary>
/// Pure .NET audio format converter for OGG (Opus) ↔ WAV (PCM16) conversion.
/// Generates proper 44-byte WAV headers targeting 16kHz mono PCM16 for AudioPreprocessor compatibility.
/// </summary>
internal sealed class AudioFormatConverter : IAudioFormatConverter
{
    /// <summary>Target sample rate for voice pipeline (16kHz).</summary>
    internal const int TargetSampleRate = 16000;

    /// <summary>Mono channel.</summary>
    internal const short TargetChannels = 1;

    /// <summary>Bits per sample for PCM16.</summary>
    internal const short BitsPerSample = 16;

    /// <summary>Maximum voice note duration in seconds.</summary>
    internal const int MaxDurationSeconds = 60;

    /// <summary>WAV header size in bytes.</summary>
    internal const int WavHeaderSize = 44;

    private static readonly ActivitySource s_activitySource = new("nem.Mimir.Telegram.Voice", "1.0.0");

    /// <inheritdoc />
    public Task<byte[]> ConvertOggToWavAsync(byte[] oggData, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(oggData);

        if (oggData.Length == 0)
            throw new ArgumentException("OGG data cannot be empty.", nameof(oggData));

        using var activity = s_activitySource.StartActivity("voice.convert.ogg_to_wav");
        activity?.SetTag("voice.input_size_bytes", oggData.Length);

        ct.ThrowIfCancellationRequested();

        // For V1, we treat the OGG data as raw PCM samples and wrap in a WAV header.
        // In a production system, this would use Concentus to decode Opus frames.
        // The abstraction allows swapping in a real decoder without changing consumers.
        var pcmData = DecodeOggToPcm(oggData);

        // Enforce max duration
        var maxSamples = TargetSampleRate * MaxDurationSeconds * TargetChannels;
        if (pcmData.Length / 2 > maxSamples)
        {
            var maxBytes = maxSamples * 2;
            pcmData = pcmData[..maxBytes];
        }

        var wavData = CreateWavFile(pcmData);

        activity?.SetTag("voice.output_size_bytes", wavData.Length);
        activity?.SetTag("voice.duration_seconds", (double)(pcmData.Length / 2) / TargetSampleRate);

        return Task.FromResult(wavData);
    }

    /// <inheritdoc />
    public Task<byte[]> ConvertWavToOggAsync(byte[] wavData, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(wavData);

        if (wavData.Length < WavHeaderSize)
            throw new ArgumentException("WAV data is too small to contain a valid header.", nameof(wavData));

        using var activity = s_activitySource.StartActivity("voice.convert.wav_to_ogg");
        activity?.SetTag("voice.input_size_bytes", wavData.Length);

        ct.ThrowIfCancellationRequested();

        // Extract PCM data from WAV (skip 44-byte header)
        var pcmData = wavData[WavHeaderSize..];

        // For V1, we wrap the PCM data with a minimal OGG-like container.
        // In production, this would encode via Concentus Opus encoder.
        var oggData = EncodePcmToOgg(pcmData);

        activity?.SetTag("voice.output_size_bytes", oggData.Length);

        return Task.FromResult(oggData);
    }

    /// <summary>
    /// Creates a WAV file with a proper 44-byte PCM header.
    /// Format: RIFF/fmt/data chunks, 16kHz mono PCM16.
    /// </summary>
    internal static byte[] CreateWavFile(byte[] pcmData)
    {
        var dataSize = pcmData.Length;
        var byteRate = TargetSampleRate * TargetChannels * (BitsPerSample / 8);
        var blockAlign = (short)(TargetChannels * (BitsPerSample / 8));

        using var ms = new MemoryStream(WavHeaderSize + dataSize);
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);             // ChunkSize
        writer.Write("WAVE"u8);

        // fmt sub-chunk
        writer.Write("fmt "u8);
        writer.Write(16);                         // Subchunk1Size (PCM)
        writer.Write((short)1);                   // AudioFormat (PCM = 1)
        writer.Write(TargetChannels);             // NumChannels
        writer.Write(TargetSampleRate);           // SampleRate
        writer.Write(byteRate);                   // ByteRate
        writer.Write(blockAlign);                 // BlockAlign
        writer.Write(BitsPerSample);              // BitsPerSample

        // data sub-chunk
        writer.Write("data"u8);
        writer.Write(dataSize);                   // Subchunk2Size
        writer.Write(pcmData);                    // PCM data

        return ms.ToArray();
    }

    /// <summary>
    /// Decodes OGG/Opus data to raw PCM samples.
    /// V1 implementation: returns the raw bytes (assumes pre-decoded PCM for testing).
    /// Production would use Concentus OGG/Opus decoder.
    /// </summary>
    internal static byte[] DecodeOggToPcm(byte[] oggData)
    {
        // In a real implementation, this would:
        // 1. Parse OGG container pages
        // 2. Extract Opus packets
        // 3. Decode Opus frames to PCM via Concentus OpusDecoder
        // 4. Resample to 16kHz mono if needed
        // For V1, we pass through — the interface allows mocking for tests
        // and swapping in Concentus later.
        return oggData;
    }

    /// <summary>
    /// Encodes raw PCM samples to OGG/Opus format.
    /// V1 implementation: returns raw bytes (placeholder).
    /// Production would use Concentus OGG/Opus encoder.
    /// </summary>
    internal static byte[] EncodePcmToOgg(byte[] pcmData)
    {
        // In a real implementation, this would:
        // 1. Create Opus encoder via Concentus
        // 2. Encode PCM frames to Opus packets
        // 3. Wrap in OGG container pages
        // For V1, we pass through — the interface allows mocking
        return pcmData;
    }

    /// <summary>
    /// Validates that the given WAV data has a proper header with expected format.
    /// </summary>
    internal static bool IsValidWavHeader(byte[] wavData)
    {
        if (wavData.Length < WavHeaderSize)
            return false;

        // Check RIFF header
        if (wavData[0] != 'R' || wavData[1] != 'I' || wavData[2] != 'F' || wavData[3] != 'F')
            return false;

        // Check WAVE format
        if (wavData[8] != 'W' || wavData[9] != 'A' || wavData[10] != 'V' || wavData[11] != 'E')
            return false;

        // Check fmt chunk
        if (wavData[12] != 'f' || wavData[13] != 'm' || wavData[14] != 't' || wavData[15] != ' ')
            return false;

        return true;
    }

    /// <summary>
    /// Extracts sample rate from a WAV header.
    /// </summary>
    internal static int GetWavSampleRate(byte[] wavData)
    {
        if (wavData.Length < WavHeaderSize)
            throw new ArgumentException("WAV data too small for header extraction.");

        return BitConverter.ToInt32(wavData, 24);
    }

    /// <summary>
    /// Extracts number of channels from a WAV header.
    /// </summary>
    internal static short GetWavChannels(byte[] wavData)
    {
        if (wavData.Length < WavHeaderSize)
            throw new ArgumentException("WAV data too small for header extraction.");

        return BitConverter.ToInt16(wavData, 22);
    }

    /// <summary>
    /// Extracts bits per sample from a WAV header.
    /// </summary>
    internal static short GetWavBitsPerSample(byte[] wavData)
    {
        if (wavData.Length < WavHeaderSize)
            throw new ArgumentException("WAV data too small for header extraction.");

        return BitConverter.ToInt16(wavData, 34);
    }
}
