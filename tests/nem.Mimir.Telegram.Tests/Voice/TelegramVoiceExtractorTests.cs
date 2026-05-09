using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using nem.Mimir.Telegram.Voice;
using NSubstitute;
using Shouldly;
using global::Telegram.Bot;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.Enums;
using global::Telegram.Bot.Requests;
using TelegramVoice = global::Telegram.Bot.Types.Voice;

namespace nem.Mimir.Telegram.Tests.Voice;

// ═══════════════════════════════════════════════════════════════════
// AudioFormatConverter Tests
// ═══════════════════════════════════════════════════════════════════

public sealed class AudioFormatConverterTests
{
    private readonly AudioFormatConverter _sut = new();

    // ─── OGG → WAV Conversion ───────────────────────────────────

    [Fact]
    public async Task ConvertOggToWavAsync_ValidOggData_ReturnsWavWithCorrectHeader()
    {
        // Arrange — fake PCM data (100 samples = 200 bytes)
        var fakePcmData = new byte[200];
        Random.Shared.NextBytes(fakePcmData);

        // Act
        var wavData = await _sut.ConvertOggToWavAsync(fakePcmData);

        // Assert — WAV header validation
        AudioFormatConverter.IsValidWavHeader(wavData).ShouldBeTrue();
        wavData.Length.ShouldBe(AudioFormatConverter.WavHeaderSize + fakePcmData.Length);
    }

    [Fact]
    public async Task ConvertOggToWavAsync_ValidOggData_Produces16kHzMonoPCM16()
    {
        // Arrange
        var fakePcmData = new byte[200];

        // Act
        var wavData = await _sut.ConvertOggToWavAsync(fakePcmData);

        // Assert — AudioPreprocessor-compatible format
        AudioFormatConverter.GetWavSampleRate(wavData).ShouldBe(16000);
        AudioFormatConverter.GetWavChannels(wavData).ShouldBe((short)1);
        AudioFormatConverter.GetWavBitsPerSample(wavData).ShouldBe((short)16);
    }

    [Fact]
    public async Task ConvertOggToWavAsync_EmptyData_ThrowsArgumentException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ConvertOggToWavAsync([]));
    }

    [Fact]
    public async Task ConvertOggToWavAsync_NullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.ConvertOggToWavAsync(null!));
    }

    [Fact]
    public async Task ConvertOggToWavAsync_ExceedsMaxDuration_TruncatesAudio()
    {
        // Arrange — create data that exceeds 60s at 16kHz mono PCM16
        // 60s * 16000 Hz * 1 channel * 2 bytes/sample = 1,920,000 bytes
        var maxPcmBytes = AudioFormatConverter.TargetSampleRate
            * AudioFormatConverter.MaxDurationSeconds
            * AudioFormatConverter.TargetChannels * 2;
        var oversizedData = new byte[maxPcmBytes + 2000]; // 2000 bytes over limit
        Random.Shared.NextBytes(oversizedData);

        // Act
        var wavData = await _sut.ConvertOggToWavAsync(oversizedData);

        // Assert — should be truncated to exactly max + header
        wavData.Length.ShouldBe(AudioFormatConverter.WavHeaderSize + maxPcmBytes);
    }

    [Fact]
    public async Task ConvertOggToWavAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var data = new byte[100];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.ConvertOggToWavAsync(data, cts.Token));
    }

    // ─── WAV → OGG Conversion ───────────────────────────────────

    [Fact]
    public async Task ConvertWavToOggAsync_ValidWavData_ReturnsOggData()
    {
        // Arrange — create minimal valid WAV
        var pcmData = new byte[100];
        Random.Shared.NextBytes(pcmData);
        var wavData = AudioFormatConverter.CreateWavFile(pcmData);

        // Act
        var oggData = await _sut.ConvertWavToOggAsync(wavData);

        // Assert — should strip header and return PCM (V1 passthrough)
        oggData.Length.ShouldBe(pcmData.Length);
    }

    [Fact]
    public async Task ConvertWavToOggAsync_TooSmallForHeader_ThrowsArgumentException()
    {
        // Arrange — less than 44 bytes
        var tinyData = new byte[20];

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ConvertWavToOggAsync(tinyData));
    }

    [Fact]
    public async Task ConvertWavToOggAsync_NullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.ConvertWavToOggAsync(null!));
    }

    // ─── WAV Header Utilities ───────────────────────────────────

    [Fact]
    public void CreateWavFile_ProducesCorrectRIFFHeader()
    {
        // Arrange
        var pcm = new byte[100];

        // Act
        var wav = AudioFormatConverter.CreateWavFile(pcm);

        // Assert
        wav[0].ShouldBe((byte)'R');
        wav[1].ShouldBe((byte)'I');
        wav[2].ShouldBe((byte)'F');
        wav[3].ShouldBe((byte)'F');
        wav[8].ShouldBe((byte)'W');
        wav[9].ShouldBe((byte)'A');
        wav[10].ShouldBe((byte)'V');
        wav[11].ShouldBe((byte)'E');
    }

    [Fact]
    public void CreateWavFile_CorrectChunkSize()
    {
        // Arrange
        var pcm = new byte[256];

        // Act
        var wav = AudioFormatConverter.CreateWavFile(pcm);

        // Assert — ChunkSize = 36 + data size
        var chunkSize = BitConverter.ToInt32(wav, 4);
        chunkSize.ShouldBe(36 + 256);
    }

    [Fact]
    public void IsValidWavHeader_ValidWav_ReturnsTrue()
    {
        var pcm = new byte[100];
        var wav = AudioFormatConverter.CreateWavFile(pcm);
        AudioFormatConverter.IsValidWavHeader(wav).ShouldBeTrue();
    }

    [Fact]
    public void IsValidWavHeader_TooSmall_ReturnsFalse()
    {
        AudioFormatConverter.IsValidWavHeader(new byte[10]).ShouldBeFalse();
    }

    [Fact]
    public void IsValidWavHeader_WrongMagic_ReturnsFalse()
    {
        var wav = new byte[44];
        wav[0] = (byte)'X'; // Wrong magic
        AudioFormatConverter.IsValidWavHeader(wav).ShouldBeFalse();
    }
}

// ═══════════════════════════════════════════════════════════════════
// TelegramVoiceExtractor Tests
// ═══════════════════════════════════════════════════════════════════

public sealed class TelegramVoiceExtractorTests
{
    private readonly IAudioFormatConverter _converter = Substitute.For<IAudioFormatConverter>();
    private readonly ILogger<TelegramVoiceExtractor> _logger = NullLogger<TelegramVoiceExtractor>.Instance;
    private readonly ITelegramBotClient _bot = Substitute.For<ITelegramBotClient>();
    private readonly TelegramVoiceExtractor _sut;

    public TelegramVoiceExtractorTests()
    {
        _sut = new TelegramVoiceExtractor(_converter, _logger);
    }

    [Fact]
    public async Task ExtractVoiceAudioAsync_ValidVoice_DownloadsAndConverts()
    {
        // Arrange
        var voice = CreateVoice("file-123", 5, "audio/ogg");
        var fakeOgg = new byte[] { 1, 2, 3, 4, 5 };
        var fakeWav = new byte[] { 10, 20, 30 };

        SetupGetFileAndDownload("file-123", "path/to/file.ogg", fakeOgg);
        _converter.ConvertOggToWavAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(fakeWav);

        // Act
        var result = await _sut.ExtractVoiceAudioAsync(_bot, voice);

        // Assert
        result.ShouldBe(fakeWav);
        await _converter.Received(1).ConvertOggToWavAsync(
            Arg.Is<byte[]>(b => b.Length == fakeOgg.Length),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractVoiceAudioAsync_NullBotClient_ThrowsArgumentNullException()
    {
        var voice = CreateVoice("file-1", 5);

        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.ExtractVoiceAudioAsync(null!, voice));
    }

    [Fact]
    public async Task ExtractVoiceAudioAsync_NullVoice_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.ExtractVoiceAudioAsync(_bot, null!));
    }

    [Fact]
    public async Task ExtractVoiceAudioAsync_EmptyFilePath_ThrowsInvalidOperationException()
    {
        // Arrange — GetFile returns empty path
        var voice = CreateVoice("file-empty", 5);
        _bot.SendRequest(Arg.Any<GetFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TGFile { FileId = "file-empty", FilePath = null });

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.ExtractVoiceAudioAsync(_bot, voice));
    }

    [Fact]
    public async Task ExtractVoiceAudioAsync_EmptyDownload_ThrowsInvalidOperationException()
    {
        // Arrange — download returns 0 bytes
        var voice = CreateVoice("file-zero", 5);
        SetupGetFileAndDownload("file-zero", "path/to/file.ogg", []);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.ExtractVoiceAudioAsync(_bot, voice));
    }

    [Fact]
    public void Constructor_NullConverter_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(
            () => new TelegramVoiceExtractor(null!, _logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(
            () => new TelegramVoiceExtractor(_converter, null!));
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private void SetupGetFileAndDownload(string fileId, string filePath, byte[] data)
    {
        _bot.SendRequest(Arg.Any<GetFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TGFile { FileId = fileId, FilePath = filePath });

        _bot.When(x => x.DownloadFile(
                Arg.Is<string>(p => p == filePath),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var stream = ci.ArgAt<Stream>(1);
                stream.Write(data, 0, data.Length);
            });
    }

    private static TelegramVoice CreateVoice(string fileId, int durationSeconds, string? mimeType = null)
    {
        return new TelegramVoice
        {
            FileId = fileId,
            FileUniqueId = $"unique-{fileId}",
            Duration = durationSeconds,
            MimeType = mimeType
        };
    }
}

// ═══════════════════════════════════════════════════════════════════
// VoiceMessageHandler Tests
// ═══════════════════════════════════════════════════════════════════

public sealed class VoiceMessageHandlerTests
{
    private readonly ITelegramVoiceExtractor _extractor = Substitute.For<ITelegramVoiceExtractor>();
    private readonly IAudioFormatConverter _converter = Substitute.For<IAudioFormatConverter>();
    private readonly ILogger<VoiceMessageHandler> _logger = NullLogger<VoiceMessageHandler>.Instance;
    private readonly ITelegramBotClient _bot = Substitute.For<ITelegramBotClient>();
    private readonly VoiceMessageHandler _sut;

    public VoiceMessageHandlerTests()
    {
        _sut = new VoiceMessageHandler(_extractor, _converter, _logger);
    }

    [Fact]
    public async Task HandleVoiceMessageAsync_ValidVoice_ExtractsAndSendsConfirmation()
    {
        // Arrange
        var voice = new TelegramVoice
        {
            FileId = "voice-file-1",
            FileUniqueId = "unique-1",
            Duration = 5
        };
        var message = CreateVoiceMessage(voice);
        var fakeWav = new byte[] { 1, 2, 3 };

        _extractor.ExtractVoiceAudioAsync(_bot, voice, Arg.Any<CancellationToken>())
            .Returns(fakeWav);

        // Act
        await _sut.HandleVoiceMessageAsync(_bot, message, CancellationToken.None);

        // Assert — extractor was called
        await _extractor.Received(1).ExtractVoiceAudioAsync(_bot, voice, Arg.Any<CancellationToken>());

        // Assert — confirmation message sent (look at all SendMessageRequest calls)
        var sentTexts = GetAllSentMessageTexts();
        sentTexts.ShouldContain(t => t.Contains("Processing your voice message"));
        sentTexts.ShouldContain(t => t.Contains("processed successfully"));
    }

    [Fact]
    public async Task HandleVoiceMessageAsync_NullVoiceProperty_ReturnsWithoutProcessing()
    {
        // Arrange — message with no Voice property
        var message = new Message
        {
            Chat = new Chat { Id = 100, Type = ChatType.Private },
            From = new User { Id = 200, IsBot = false, FirstName = "Test" },
            Date = DateTime.UtcNow
        };

        // Act
        await _sut.HandleVoiceMessageAsync(_bot, message, CancellationToken.None);

        // Assert — extractor should NOT be called
        await _extractor.DidNotReceive().ExtractVoiceAudioAsync(
            Arg.Any<ITelegramBotClient>(),
            Arg.Any<TelegramVoice>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleVoiceMessageAsync_ExtractionFails_SendsErrorMessage()
    {
        // Arrange
        var voice = new TelegramVoice
        {
            FileId = "voice-fail",
            FileUniqueId = "unique-fail",
            Duration = 3
        };
        var message = CreateVoiceMessage(voice);

        _extractor.ExtractVoiceAudioAsync(_bot, voice, Arg.Any<CancellationToken>())
            .Returns<byte[]>(_ => throw new InvalidOperationException("Download failed"));

        // Act
        await _sut.HandleVoiceMessageAsync(_bot, message, CancellationToken.None);

        // Assert — error message sent
        var sentTexts = GetAllSentMessageTexts();
        sentTexts.ShouldContain(t => t.Contains("Failed to process your voice message"));
    }

    [Fact]
    public async Task HandleVoiceMessageAsync_NullBotClient_ThrowsArgumentNullException()
    {
        var message = CreateVoiceMessage(new TelegramVoice
        {
            FileId = "f",
            FileUniqueId = "u",
            Duration = 1
        });

        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.HandleVoiceMessageAsync(null!, message, CancellationToken.None));
    }

    [Fact]
    public async Task HandleVoiceMessageAsync_NullMessage_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.HandleVoiceMessageAsync(_bot, null!, CancellationToken.None));
    }

    // ─── SendVoiceResponse ──────────────────────────────────────

    [Fact]
    public async Task SendVoiceResponseAsync_ValidWav_ConvertsAndSendsVoice()
    {
        // Arrange
        var wavData = new byte[] { 1, 2, 3, 4, 5 };
        var oggData = new byte[] { 10, 20, 30 };

        _converter.ConvertWavToOggAsync(wavData, Arg.Any<CancellationToken>())
            .Returns(oggData);

        // Act
        await _sut.SendVoiceResponseAsync(_bot, 100, wavData, CancellationToken.None);

        // Assert — converter was called
        await _converter.Received(1).ConvertWavToOggAsync(wavData, Arg.Any<CancellationToken>());

        // Assert — SendVoice was called (check for SendVoiceRequest)
        var calls = _bot.ReceivedCalls();
        calls.ShouldContain(c =>
            c.GetArguments().Length > 0 &&
            c.GetArguments()[0] is SendVoiceRequest);
    }

    [Fact]
    public async Task SendVoiceResponseAsync_NullBotClient_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SendVoiceResponseAsync(null!, 100, [1, 2, 3], CancellationToken.None));
    }

    [Fact]
    public async Task SendVoiceResponseAsync_NullWavData_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SendVoiceResponseAsync(_bot, 100, null!, CancellationToken.None));
    }

    [Fact]
    public void Constructor_NullExtractor_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(
            () => new VoiceMessageHandler(null!, _converter, _logger));
    }

    [Fact]
    public void Constructor_NullConverter_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(
            () => new VoiceMessageHandler(_extractor, null!, _logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(
            () => new VoiceMessageHandler(_extractor, _converter, null!));
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private static Message CreateVoiceMessage(TelegramVoice voice)
    {
        return new Message
        {
            Voice = voice,
            Chat = new Chat { Id = 100, Type = ChatType.Private },
            From = new User { Id = 200, IsBot = false, FirstName = "Test" },
            Date = DateTime.UtcNow
        };
    }

    private List<string> GetAllSentMessageTexts()
    {
        var texts = new List<string>();
        foreach (var call in _bot.ReceivedCalls())
        {
            var args = call.GetArguments();
            if (args.Length > 0 && args[0] is SendMessageRequest request)
            {
                texts.Add(request.Text);
            }
        }
        return texts;
    }
}
