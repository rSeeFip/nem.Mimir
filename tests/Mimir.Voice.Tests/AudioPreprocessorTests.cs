namespace Mimir.Voice.Tests;

using Shouldly;

public class AudioPreprocessorTests
{
    private readonly AudioPreprocessor _sut = new();

    /// <summary>
    /// Creates a valid WAV file in memory with the given parameters.
    /// </summary>
    private static MemoryStream CreateWavStream(
        int sampleRate = 16000,
        short channels = 1,
        short bitsPerSample = 16,
        double durationSeconds = 1.0,
        short audioFormat = 1)
    {
        var numSamples = (int)(sampleRate * durationSeconds * channels);
        var bytesPerSample = bitsPerSample / 8;
        var dataSize = numSamples * bytesPerSample;

        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // RIFF header
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataSize); // file size - 8
        writer.Write("WAVE".ToCharArray());

        // fmt chunk
        writer.Write("fmt ".ToCharArray());
        writer.Write(16); // chunk size
        writer.Write(audioFormat); // PCM = 1
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bytesPerSample); // byte rate
        writer.Write((short)(channels * bytesPerSample)); // block align
        writer.Write(bitsPerSample);

        // data chunk
        writer.Write("data".ToCharArray());
        writer.Write(dataSize);

        // Write silent PCM data
        for (int i = 0; i < numSamples; i++)
        {
            writer.Write((short)0);
        }

        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Process_ValidWav_ReturnsSuccess()
    {
        using var stream = CreateWavStream(durationSeconds: 2.0);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeTrue();
        result.Samples.ShouldNotBeNull();
        result.SampleRate.ShouldBe(16000);
        result.Channels.ShouldBe(1);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Process_ValidWav_ExtractsSamplesCorrectly()
    {
        using var stream = CreateWavStream(durationSeconds: 1.0);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeTrue();
        result.Samples!.Length.ShouldBe(16000);
        // Silent audio should produce all-zero samples
        result.Samples.ShouldAllBe(s => s == 0f);
    }

    [Fact]
    public void Process_ValidWav_CalculatesDurationCorrectly()
    {
        using var stream = CreateWavStream(durationSeconds: 5.0);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeTrue();
        result.Duration.TotalSeconds.ShouldBe(5.0, 0.1);
    }

    [Fact]
    public void Process_EmptyStream_ReturnsFailure()
    {
        using var stream = new MemoryStream();

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("empty");
    }

    [Fact]
    public void Process_NullStream_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Process(null!, 60));
    }

    [Fact]
    public void Process_TooShortForHeader_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[10]);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("too short");
    }

    [Fact]
    public void Process_InvalidRiffHeader_ReturnsFailure()
    {
        var data = new byte[100];
        data[0] = (byte)'X';
        data[1] = (byte)'Y';
        data[2] = (byte)'Z';
        data[3] = (byte)'Z';
        using var stream = new MemoryStream(data);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("RIFF");
    }

    [Fact]
    public void Process_MissingWaveIdentifier_ReturnsFailure()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write("RIFF".ToCharArray());
        writer.Write(0); // file size
        writer.Write("XXXX".ToCharArray()); // not WAVE
        // Pad to 44 bytes
        writer.Write(new byte[32]);
        writer.Flush();
        stream.Position = 0;

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("WAVE");
    }

    [Fact]
    public void Process_WrongSampleRate_ReturnsFailure()
    {
        using var stream = CreateWavStream(sampleRate: 44100);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("sample rate");
        result.Error!.ShouldContain("44100");
    }

    [Fact]
    public void Process_StereoAudio_ReturnsFailure()
    {
        using var stream = CreateWavStream(channels: 2);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("channel");
        result.Error!.ShouldContain("mono");
    }

    [Fact]
    public void Process_Non16BitAudio_ReturnsFailure()
    {
        using var stream = CreateWavStream(bitsPerSample: 24);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("bits per sample");
    }

    [Fact]
    public void Process_NonPcmFormat_ReturnsFailure()
    {
        using var stream = CreateWavStream(audioFormat: 3); // IEEE float

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("PCM");
    }

    [Fact]
    public void Process_DurationExceedsMax_ReturnsFailure()
    {
        using var stream = CreateWavStream(durationSeconds: 61.0);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("exceeds maximum");
        result.Error!.ShouldContain("60");
    }

    [Fact]
    public void Process_DurationExactlyAtMax_ReturnsSuccess()
    {
        using var stream = CreateWavStream(durationSeconds: 60.0);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Process_ShortDuration_ReturnsSuccess()
    {
        using var stream = CreateWavStream(durationSeconds: 0.5);

        var result = _sut.Process(stream, 60);

        result.IsValid.ShouldBeTrue();
        result.Duration.TotalSeconds.ShouldBe(0.5, 0.1);
    }
}
