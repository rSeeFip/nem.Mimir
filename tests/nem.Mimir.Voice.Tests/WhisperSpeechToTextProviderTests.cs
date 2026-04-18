namespace nem.Mimir.Voice.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Voice.Configuration;
using NSubstitute;
using Shouldly;

public class WhisperSpeechToTextProviderTests
{
    private readonly IWhisperProcessorFactory _processorFactory;
    private readonly IWhisperProcessorWrapper _processorWrapper;
    private readonly AudioPreprocessor _preprocessor;
    private readonly IOptions<WhisperOptions> _options;
    private readonly ILogger<WhisperSpeechToTextProvider> _logger;
    private readonly WhisperSpeechToTextProvider _sut;

    public WhisperSpeechToTextProviderTests()
    {
        _processorFactory = Substitute.For<IWhisperProcessorFactory>();
        _processorWrapper = Substitute.For<IWhisperProcessorWrapper>();
        _preprocessor = new AudioPreprocessor();
        _options = Options.Create(new WhisperOptions
        {
            ModelPath = "/tmp/model.bin",
            MaxDurationSeconds = 60,
            TimeoutSeconds = 30
        });
        _logger = Substitute.For<ILogger<WhisperSpeechToTextProvider>>();

        _processorFactory.CreateProcessor(Arg.Any<string?>())
            .Returns(_processorWrapper);

        _sut = new WhisperSpeechToTextProvider(
            _processorFactory,
            _preprocessor,
            _options,
            _logger);
    }

    /// <summary>
    /// Creates a valid 16kHz mono PCM16 WAV stream.
    /// </summary>
    private static MemoryStream CreateValidWavStream(double durationSeconds = 1.0)
    {
        const int sampleRate = 16000;
        const short channels = 1;
        const short bitsPerSample = 16;
        var numSamples = (int)(sampleRate * durationSeconds * channels);
        var bytesPerSample = bitsPerSample / 8;
        var dataSize = numSamples * bytesPerSample;

        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE".ToCharArray());
        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bytesPerSample);
        writer.Write((short)(channels * bytesPerSample));
        writer.Write(bitsPerSample);
        writer.Write("data".ToCharArray());
        writer.Write(dataSize);

        for (int i = 0; i < numSamples; i++)
        {
            writer.Write((short)0);
        }

        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    private static async IAsyncEnumerable<TranscriptionSegment> CreateSegments(
        params TranscriptionSegment[] segments)
    {
        foreach (var segment in segments)
        {
            yield return segment;
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task TranscribeAsync_ValidAudio_ReturnsTranscriptionResult()
    {
        using var stream = CreateValidWavStream(2.0);

        _processorWrapper.ProcessAsync(Arg.Any<float[]>(), Arg.Any<CancellationToken>())
            .Returns(CreateSegments(
                new TranscriptionSegment("Hello ", TimeSpan.Zero, TimeSpan.FromSeconds(1)),
                new TranscriptionSegment("World", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))));

        var result = await _sut.TranscribeAsync(stream, "en", TestContext.Current.CancellationToken);

        result.Text.ShouldBe("Hello World");
        result.Language.ShouldBe("en");
        result.Duration.TotalSeconds.ShouldBe(2.0, 0.1);
        result.Confidence.ShouldBe(1.0);
    }

    [Fact]
    public async Task TranscribeAsync_NoLanguageHint_UsesConfigLanguage()
    {
        var options = Options.Create(new WhisperOptions
        {
            ModelPath = "/tmp/model.bin",
            Language = "es",
            MaxDurationSeconds = 60,
            TimeoutSeconds = 30
        });

        var sut = new WhisperSpeechToTextProvider(
            _processorFactory, _preprocessor, options, _logger);

        using var stream = CreateValidWavStream();

        _processorWrapper.ProcessAsync(Arg.Any<float[]>(), Arg.Any<CancellationToken>())
            .Returns(CreateSegments(
                new TranscriptionSegment("Hola", TimeSpan.Zero, TimeSpan.FromSeconds(1))));

        var result = await sut.TranscribeAsync(stream, ct: TestContext.Current.CancellationToken);

        _processorFactory.Received(1).CreateProcessor("es");
        result.Language.ShouldBe("es");
    }

    [Fact]
    public async Task TranscribeAsync_LanguageHintOverridesConfig()
    {
        var options = Options.Create(new WhisperOptions
        {
            ModelPath = "/tmp/model.bin",
            Language = "es",
            MaxDurationSeconds = 60,
            TimeoutSeconds = 30
        });

        var sut = new WhisperSpeechToTextProvider(
            _processorFactory, _preprocessor, options, _logger);

        using var stream = CreateValidWavStream();

        _processorWrapper.ProcessAsync(Arg.Any<float[]>(), Arg.Any<CancellationToken>())
            .Returns(CreateSegments(
                new TranscriptionSegment("Hello", TimeSpan.Zero, TimeSpan.FromSeconds(1))));

        var result = await sut.TranscribeAsync(stream, "en", TestContext.Current.CancellationToken);

        _processorFactory.Received(1).CreateProcessor("en");
        result.Language.ShouldBe("en");
    }

    [Fact]
    public async Task TranscribeAsync_NullLanguage_AutoDetects()
    {
        using var stream = CreateValidWavStream();

        _processorWrapper.ProcessAsync(Arg.Any<float[]>(), Arg.Any<CancellationToken>())
            .Returns(CreateSegments(
                new TranscriptionSegment("Hello", TimeSpan.Zero, TimeSpan.FromSeconds(1))));

        var result = await _sut.TranscribeAsync(stream, ct: TestContext.Current.CancellationToken);

        _processorFactory.Received(1).CreateProcessor(null);
    }

    [Fact]
    public async Task TranscribeAsync_EmptyResult_ReturnsEmptyTextWithZeroConfidence()
    {
        using var stream = CreateValidWavStream();

        _processorWrapper.ProcessAsync(Arg.Any<float[]>(), Arg.Any<CancellationToken>())
            .Returns(CreateSegments());

        var result = await _sut.TranscribeAsync(stream, ct: TestContext.Current.CancellationToken);

        result.Text.ShouldBeEmpty();
        result.Confidence.ShouldBe(0.0);
    }

    [Fact]
    public async Task TranscribeAsync_ExceedsMaxDuration_ThrowsInvalidOperation()
    {
        using var stream = CreateValidWavStream(61.0);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.TranscribeAsync(stream, ct: TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("exceeds maximum");
    }

    [Fact]
    public async Task TranscribeAsync_InvalidWavFormat_ThrowsInvalidOperation()
    {
        // Create a non-WAV stream
        using var stream = new MemoryStream(new byte[100]);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.TranscribeAsync(stream, ct: TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("preprocessing failed");
    }

    [Fact]
    public async Task TranscribeAsync_EmptyStream_ThrowsInvalidOperation()
    {
        using var stream = new MemoryStream();

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.TranscribeAsync(stream, ct: TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("preprocessing failed");
    }

    [Fact]
    public async Task TranscribeAsync_NullStream_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.TranscribeAsync(null!, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TranscribeAsync_Timeout_ThrowsTimeoutException()
    {
        var options = Options.Create(new WhisperOptions
        {
            ModelPath = "/tmp/model.bin",
            MaxDurationSeconds = 60,
            TimeoutSeconds = 1 // Very short timeout
        });

        var sut = new WhisperSpeechToTextProvider(
            _processorFactory, _preprocessor, options, _logger);

        using var stream = CreateValidWavStream(2.0);

        // Simulate slow processing that respects cancellation token
        _processorWrapper.ProcessAsync(Arg.Any<float[]>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(1);
                return SlowSegments(ct);
            });

        await Should.ThrowAsync<TimeoutException>(
            () => sut.TranscribeAsync(stream, ct: TestContext.Current.CancellationToken));
    }

    private static async IAsyncEnumerable<TranscriptionSegment> SlowSegments(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Wait long enough for timeout to trigger
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        yield return new TranscriptionSegment("Late", TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TranscribeAsync_CancellationRequested_Propagates()
    {
        using var stream = CreateValidWavStream();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _processorWrapper.ProcessAsync(Arg.Any<float[]>(), Arg.Any<CancellationToken>())
            .Returns(_ => ThrowingSegments());

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.TranscribeAsync(stream, ct: cts.Token));
    }

    private static async IAsyncEnumerable<TranscriptionSegment> ThrowingSegments()
    {
        await Task.CompletedTask;
        if (false)
        {
            throw new OperationCanceledException();
        }

        yield break;
    }

    [Fact]
    public async Task TranscribeAsync_MultipleSegments_ConcatenatesText()
    {
        using var stream = CreateValidWavStream(3.0);

        _processorWrapper.ProcessAsync(Arg.Any<float[]>(), Arg.Any<CancellationToken>())
            .Returns(CreateSegments(
                new TranscriptionSegment(" Hello ", TimeSpan.Zero, TimeSpan.FromSeconds(1)),
                new TranscriptionSegment("beautiful ", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
                new TranscriptionSegment("world ", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3))));

        var result = await _sut.TranscribeAsync(stream, ct: TestContext.Current.CancellationToken);

        result.Text.ShouldBe("Hello beautiful world");
    }

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => new WhisperSpeechToTextProvider(
            null!, _preprocessor, _options, _logger));
    }

    [Fact]
    public void Constructor_NullPreprocessor_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => new WhisperSpeechToTextProvider(
            _processorFactory, null!, _options, _logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => new WhisperSpeechToTextProvider(
            _processorFactory, _preprocessor, null!, _logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Should.Throw<ArgumentNullException>(() => new WhisperSpeechToTextProvider(
            _processorFactory, _preprocessor, _options, null!));
    }

    [Fact]
    public async Task TranscribeAsync_CreatesProcessorWithLanguage()
    {
        using var stream = CreateValidWavStream();

        _processorWrapper.ProcessAsync(Arg.Any<float[]>(), Arg.Any<CancellationToken>())
            .Returns(CreateSegments(
                new TranscriptionSegment("Bonjour", TimeSpan.Zero, TimeSpan.FromSeconds(1))));

        await _sut.TranscribeAsync(stream, "fr", TestContext.Current.CancellationToken);

        _processorFactory.Received(1).CreateProcessor("fr");
    }

    [Fact]
    public async Task TranscribeAsync_DisposesProcessor()
    {
        using var stream = CreateValidWavStream();

        _processorWrapper.ProcessAsync(Arg.Any<float[]>(), Arg.Any<CancellationToken>())
            .Returns(CreateSegments(
                new TranscriptionSegment("Hello", TimeSpan.Zero, TimeSpan.FromSeconds(1))));

        await _sut.TranscribeAsync(stream, ct: TestContext.Current.CancellationToken);

        _processorWrapper.Received(1).Dispose();
    }
}
