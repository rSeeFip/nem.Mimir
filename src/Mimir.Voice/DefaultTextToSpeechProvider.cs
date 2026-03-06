using System.Buffers;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Voice.Configuration;
using nem.Contracts.Voice;

namespace Mimir.Voice;

/// <summary>
/// Default implementation of <see cref="ITextToSpeechProvider"/> that uses a local
/// piper-tts backend via <see cref="ITtsBackend"/> abstraction.
/// Outputs 16kHz mono 16-bit PCM WAV audio with 1000 char text truncation.
/// </summary>
internal sealed class DefaultTextToSpeechProvider : ITextToSpeechProvider
{
    private const int ChunkSize = 4096;
    private const int BitsPerSample = 16;
    private const int NumChannels = 1;

    private readonly ITtsBackend _backend;
    private readonly ILogger<DefaultTextToSpeechProvider> _logger;
    private readonly TtsOptions _options;

    public DefaultTextToSpeechProvider(
        ITtsBackend backend,
        ILogger<DefaultTextToSpeechProvider> logger,
        IOptions<TtsOptions> options)
    {
        _backend = backend;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<byte[]> SynthesizeAsync(
        string text,
        string? voiceId = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var fullAudio = await SynthesizeFullAsync(text, voiceId, ct);

        // Yield in chunks of ChunkSize bytes
        var offset = 0;
        while (offset < fullAudio.Length)
        {
            ct.ThrowIfCancellationRequested();

            var remaining = fullAudio.Length - offset;
            var size = Math.Min(ChunkSize, remaining);
            var chunk = new byte[size];
            Buffer.BlockCopy(fullAudio, offset, chunk, 0, size);
            yield return chunk;
            offset += size;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> SynthesizeFullAsync(
        string text,
        string? voiceId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var processedText = TruncateIfNeeded(text);
        var voice = voiceId ?? _options.DefaultVoiceId;

        _logger.LogDebug(
            "Synthesizing {CharCount} characters with voice {VoiceId}",
            processedText.Length,
            voice);

        var rawPcm = await _backend.GenerateAudioAsync(processedText, voice, ct);

        return CreateWavFile(rawPcm, _options.SampleRate, NumChannels, BitsPerSample);
    }

    /// <summary>
    /// Truncates text to the configured maximum character limit, logging a warning if truncation occurs.
    /// </summary>
    internal string TruncateIfNeeded(string text)
    {
        if (text.Length <= _options.MaxCharacters)
        {
            return text;
        }

        _logger.LogWarning(
            "Text length {TextLength} exceeds maximum of {MaxCharacters} characters. Truncating.",
            text.Length,
            _options.MaxCharacters);

        return text[.._options.MaxCharacters];
    }

    /// <summary>
    /// Creates a valid WAV file from raw PCM data with the appropriate header.
    /// </summary>
    internal static byte[] CreateWavFile(byte[] rawPcm, int sampleRate, int channels, int bitsPerSample)
    {
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var dataSize = rawPcm.Length;
        var fileSize = 36 + dataSize; // 44 byte header - 8 bytes for RIFF chunk descriptor

        using var stream = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(stream);

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(fileSize);
        writer.Write("WAVE"u8);

        // fmt sub-chunk
        writer.Write("fmt "u8);
        writer.Write(16);                       // Sub-chunk size (16 for PCM)
        writer.Write((short)1);                 // Audio format (1 = PCM)
        writer.Write((short)channels);          // Number of channels
        writer.Write(sampleRate);               // Sample rate
        writer.Write(byteRate);                 // Byte rate
        writer.Write(blockAlign);               // Block align
        writer.Write((short)bitsPerSample);     // Bits per sample

        // data sub-chunk
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(rawPcm);

        return stream.ToArray();
    }
}
