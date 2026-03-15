using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Voice;
using nem.Mimir.Voice.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace nem.Mimir.Voice.Tests;

public class DefaultTextToSpeechProviderTests
{
    private readonly ITtsBackend _backend;
    private readonly ILogger<DefaultTextToSpeechProvider> _logger;
    private readonly TtsOptions _options;
    private readonly DefaultTextToSpeechProvider _sut;

    public DefaultTextToSpeechProviderTests()
    {
        _backend = Substitute.For<ITtsBackend>();
        _logger = Substitute.For<ILogger<DefaultTextToSpeechProvider>>();
        _options = new TtsOptions
        {
            ModelPath = "/models/test.onnx",
            DefaultVoiceId = "test-voice",
            MaxCharacters = 1000,
            SampleRate = 16000
        };
        var optionsWrapper = Substitute.For<IOptions<TtsOptions>>();
        optionsWrapper.Value.Returns(_options);

        _sut = new DefaultTextToSpeechProvider(_backend, _logger, optionsWrapper);
    }

    [Fact]
    public async Task SynthesizeFullAsync_NormalText_ReturnsWavWithValidHeader()
    {
        // Arrange
        var text = "Hello world";
        var rawPcm = new byte[3200]; // 100ms of 16kHz 16-bit mono
        Random.Shared.NextBytes(rawPcm);

        _backend.GenerateAudioAsync(text, "test-voice", Arg.Any<CancellationToken>())
            .Returns(rawPcm);

        // Act
        var result = await _sut.SynthesizeFullAsync(text);

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(44 + rawPcm.Length); // 44 byte WAV header + PCM data

        // Validate WAV header
        result[0].ShouldBe((byte)'R');
        result[1].ShouldBe((byte)'I');
        result[2].ShouldBe((byte)'F');
        result[3].ShouldBe((byte)'F');

        // WAVE identifier
        result[8].ShouldBe((byte)'W');
        result[9].ShouldBe((byte)'A');
        result[10].ShouldBe((byte)'V');
        result[11].ShouldBe((byte)'E');

        // Audio format = 1 (PCM)
        var audioFormat = BitConverter.ToInt16(result, 20);
        audioFormat.ShouldBe((short)1);

        // Channels = 1 (mono)
        var channels = BitConverter.ToInt16(result, 22);
        channels.ShouldBe((short)1);

        // Sample rate = 16000
        var sampleRate = BitConverter.ToInt32(result, 24);
        sampleRate.ShouldBe(16000);

        // Bits per sample = 16
        var bitsPerSample = BitConverter.ToInt16(result, 34);
        bitsPerSample.ShouldBe((short)16);
    }

    [Fact]
    public async Task SynthesizeFullAsync_WithVoiceId_UsesProvidedVoice()
    {
        // Arrange
        var rawPcm = new byte[100];
        _backend.GenerateAudioAsync("text", "custom-voice", Arg.Any<CancellationToken>())
            .Returns(rawPcm);

        // Act
        await _sut.SynthesizeFullAsync("text", "custom-voice");

        // Assert
        await _backend.Received(1).GenerateAudioAsync("text", "custom-voice", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SynthesizeFullAsync_WithoutVoiceId_UsesDefaultVoice()
    {
        // Arrange
        var rawPcm = new byte[100];
        _backend.GenerateAudioAsync("text", "test-voice", Arg.Any<CancellationToken>())
            .Returns(rawPcm);

        // Act
        await _sut.SynthesizeFullAsync("text");

        // Assert
        await _backend.Received(1).GenerateAudioAsync("text", "test-voice", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SynthesizeFullAsync_TextExceedsMaxChars_TruncatesAndLogsWarning()
    {
        // Arrange
        _options.MaxCharacters = 10;
        var longText = new string('A', 50);
        var expectedTruncated = new string('A', 10);
        var rawPcm = new byte[100];

        _backend.GenerateAudioAsync(expectedTruncated, "test-voice", Arg.Any<CancellationToken>())
            .Returns(rawPcm);

        // Act
        var result = await _sut.SynthesizeFullAsync(longText);

        // Assert
        result.ShouldNotBeNull();
        await _backend.Received(1).GenerateAudioAsync(expectedTruncated, "test-voice", Arg.Any<CancellationToken>());

        // Verify warning was logged
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("exceeds maximum")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SynthesizeFullAsync_TextAtMaxChars_DoesNotTruncate()
    {
        // Arrange
        _options.MaxCharacters = 10;
        var text = new string('A', 10);
        var rawPcm = new byte[100];

        _backend.GenerateAudioAsync(text, "test-voice", Arg.Any<CancellationToken>())
            .Returns(rawPcm);

        // Act
        await _sut.SynthesizeFullAsync(text);

        // Assert
        await _backend.Received(1).GenerateAudioAsync(text, "test-voice", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SynthesizeFullAsync_EmptyOrWhitespaceText_ThrowsArgumentException(string? text)
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.SynthesizeFullAsync(text!));
    }

    [Fact]
    public async Task SynthesizeFullAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _backend.GenerateAudioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.SynthesizeFullAsync("hello", ct: cts.Token));
    }

    [Fact]
    public async Task SynthesizeAsync_ReturnsChunkedStream()
    {
        // Arrange
        var rawPcm = new byte[10000]; // Will produce WAV of 10044 bytes
        Random.Shared.NextBytes(rawPcm);

        _backend.GenerateAudioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(rawPcm);

        // Act
        var chunks = new List<byte[]>();
        await foreach (var chunk in _sut.SynthesizeAsync("hello"))
        {
            chunks.Add(chunk);
        }

        // Assert - total reconstructed should match full output
        var totalBytes = chunks.Sum(c => c.Length);
        totalBytes.ShouldBe(44 + rawPcm.Length);

        // All chunks except possibly the last should be 4096 bytes
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            chunks[i].Length.ShouldBe(4096);
        }
    }

    [Fact]
    public async Task SynthesizeAsync_SmallAudio_ReturnsSingleChunk()
    {
        // Arrange
        var rawPcm = new byte[100]; // Very small audio

        _backend.GenerateAudioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(rawPcm);

        // Act
        var chunks = new List<byte[]>();
        await foreach (var chunk in _sut.SynthesizeAsync("hi"))
        {
            chunks.Add(chunk);
        }

        // Assert - 144 bytes (44 header + 100 data) fits in one 4096-byte chunk
        chunks.Count.ShouldBe(1);
        chunks[0].Length.ShouldBe(144);
    }

    [Fact]
    public void TruncateIfNeeded_ShortText_ReturnsUnchanged()
    {
        // Arrange
        var text = "short text";

        // Act
        var result = _sut.TruncateIfNeeded(text);

        // Assert
        result.ShouldBe(text);
    }

    [Fact]
    public void TruncateIfNeeded_LongText_TruncatesToMaxChars()
    {
        // Arrange
        _options.MaxCharacters = 5;
        var text = "Hello World";

        // Act
        var result = _sut.TruncateIfNeeded(text);

        // Assert
        result.ShouldBe("Hello");
        result.Length.ShouldBe(5);
    }

    [Fact]
    public void CreateWavFile_ProducesValidWavHeader()
    {
        // Arrange
        var rawPcm = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var wav = DefaultTextToSpeechProvider.CreateWavFile(rawPcm, 16000, 1, 16);

        // Assert
        wav.Length.ShouldBe(48); // 44 header + 4 data

        // File size field (bytes 4-7) = total size - 8
        var fileSize = BitConverter.ToInt32(wav, 4);
        fileSize.ShouldBe(40); // 48 - 8

        // Data chunk size (bytes 40-43) = data length
        var dataSize = BitConverter.ToInt32(wav, 40);
        dataSize.ShouldBe(4);

        // Byte rate = 16000 * 1 * 16/8 = 32000
        var byteRate = BitConverter.ToInt32(wav, 28);
        byteRate.ShouldBe(32000);

        // Block align = 1 * 16/8 = 2
        var blockAlign = BitConverter.ToInt16(wav, 32);
        blockAlign.ShouldBe((short)2);
    }

    [Fact]
    public async Task SynthesizeFullAsync_BackendThrows_PropagatesException()
    {
        // Arrange
        _backend.GenerateAudioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Backend failed"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.SynthesizeFullAsync("test text"));
    }
}
