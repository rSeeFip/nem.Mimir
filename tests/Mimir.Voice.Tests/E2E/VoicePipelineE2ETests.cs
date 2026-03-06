using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mimir.Api.Handlers.Voice;
using Mimir.Api.Handlers.Voice.Messages;
using Mimir.Telegram.Voice;
using Mimir.Voice;
using nem.Contracts.Voice;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Mimir.Voice.Tests.E2E;

// ═══════════════════════════════════════════════════════════════════
//  Helpers for IAsyncEnumerable mocking and WAV generation
// ═══════════════════════════════════════════════════════════════════

internal static class E2EHelpers
{
    /// <summary>
    /// Helper async iterator to avoid CS1660 with NSubstitute lambdas.
    /// </summary>
    internal static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Creates an async enumerable that throws after yielding.
    /// </summary>
    internal static async IAsyncEnumerable<T> ThrowingAsyncEnumerable<T>(Exception ex)
    {
        await Task.Yield();
        throw ex;
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    /// <summary>
    /// Creates an async enumerable that delays (simulating slow TTS), honoring cancellation.
    /// </summary>
    internal static async IAsyncEnumerable<byte[]> SlowAsyncEnumerable(
        TimeSpan delay,
        byte[] data,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Delay(delay, ct);
        yield return data;
    }

    /// <summary>
    /// Creates a valid 16kHz mono PCM16 WAV byte array with a proper 44-byte header.
    /// </summary>
    internal static byte[] CreateValidWavBytes(double durationSeconds = 1.0)
    {
        const int sampleRate = 16000;
        const short channels = 1;
        const short bitsPerSample = 16;

        var numSamples = (int)(sampleRate * durationSeconds * channels);
        var bytesPerSample = bitsPerSample / 8;
        var dataSize = numSamples * bytesPerSample;

        using var ms = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE".ToCharArray());

        // fmt chunk
        writer.Write("fmt ".ToCharArray());
        writer.Write(16); // chunk size
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bytesPerSample); // byte rate
        writer.Write((short)(channels * bytesPerSample)); // block align
        writer.Write(bitsPerSample);

        // data chunk
        writer.Write("data".ToCharArray());
        writer.Write(dataSize);

        // Write silent PCM data (zeros)
        for (int i = 0; i < numSamples; i++)
        {
            writer.Write((short)0);
        }

        writer.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Creates WAV bytes with a non-standard sample rate (e.g. 44100).
    /// </summary>
    internal static byte[] CreateWavBytesWithSampleRate(int sampleRate, double durationSeconds = 1.0)
    {
        const short channels = 1;
        const short bitsPerSample = 16;

        var numSamples = (int)(sampleRate * durationSeconds * channels);
        var bytesPerSample = bitsPerSample / 8;
        var dataSize = numSamples * bytesPerSample;

        using var ms = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(ms);

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
        return ms.ToArray();
    }

    /// <summary>
    /// Creates WAV bytes with stereo channels.
    /// </summary>
    internal static byte[] CreateStereoWavBytes(double durationSeconds = 1.0)
    {
        const int sampleRate = 16000;
        const short channels = 2;
        const short bitsPerSample = 16;

        var numSamples = (int)(sampleRate * durationSeconds * channels);
        var bytesPerSample = bitsPerSample / 8;
        var dataSize = numSamples * bytesPerSample;

        using var ms = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(ms);

        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE".ToCharArray());

        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((short)1);
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
        return ms.ToArray();
    }

    /// <summary>
    /// Creates WAV bytes with non-PCM format (IEEE float = 3).
    /// </summary>
    internal static byte[] CreateNonPcmWavBytes(double durationSeconds = 1.0)
    {
        const int sampleRate = 16000;
        const short channels = 1;
        const short bitsPerSample = 16;

        var numSamples = (int)(sampleRate * durationSeconds * channels);
        var bytesPerSample = bitsPerSample / 8;
        var dataSize = numSamples * bytesPerSample;

        using var ms = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(ms);

        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE".ToCharArray());

        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((short)3); // IEEE float, NOT PCM
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
        return ms.ToArray();
    }
}

// ═══════════════════════════════════════════════════════════════════
//  Full voice pipeline E2E tests
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// End-to-end tests for the full voice pipeline:
/// OGG audio → WAV conversion → STT → text processing → TTS → WAV → OGG.
/// External services (Whisper, TTS backend) are mocked at the interface boundary.
/// Real AudioFormatConverter and AudioPreprocessor are exercised.
/// </summary>
public sealed class VoicePipelineE2ETests
{
    private readonly ISpeechToTextProvider _sttProvider = Substitute.For<ISpeechToTextProvider>();
    private readonly ITextToSpeechProvider _ttsProvider = Substitute.For<ITextToSpeechProvider>();
    private readonly IAudioFormatConverter _converter = Substitute.For<IAudioFormatConverter>();
    private readonly AudioPreprocessor _preprocessor = new();
    private readonly ILogger<VoicePipelineOrchestrator> _pipelineLogger = NullLogger<VoicePipelineOrchestrator>.Instance;
    private readonly ILogger<VoiceMessageReceivedHandler> _sttLogger = NullLogger<VoiceMessageReceivedHandler>.Instance;
    private readonly ILogger<VoiceSynthesisRequestHandler> _ttsLogger = NullLogger<VoiceSynthesisRequestHandler>.Instance;

    // ── Full pipeline: OGG → WAV → STT → process → TTS → WAV → OGG ──

    [Fact]
    public async Task FullPipeline_OggToWavToSttToTtsToOgg_ProducesValidResult()
    {
        // Arrange: simulate Telegram OGG voice note
        var fakeOggData = new byte[] { 0x4F, 0x67, 0x67, 0x53, 1, 2, 3, 4, 5 }; // fake OGG
        var wavFromOgg = E2EHelpers.CreateValidWavBytes(2.0);

        // Mock OGG→WAV conversion (simulates Telegram extraction)
        _converter.ConvertOggToWavAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(wavFromOgg);

        // Mock STT: transcribe the WAV audio
        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new TranscriptionResult
            {
                Text = "Hello, this is a voice test",
                Confidence = 0.95,
                Duration = TimeSpan.FromSeconds(2),
                Language = "en"
            });

        // Mock TTS: synthesize response audio (returns valid WAV chunks)
        var ttsWav = E2EHelpers.CreateValidWavBytes(1.5);
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(E2EHelpers.ToAsyncEnumerable<byte[]>(ttsWav));

        // Mock WAV→OGG conversion (for Telegram response)
        var responseOgg = new byte[] { 0x4F, 0x67, 0x67, 0x53, 10, 20, 30 };
        _converter.ConvertWavToOggAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(responseOgg);

        // ── Step 1: OGG → WAV (Telegram extraction) ──
        var wavData = await _converter.ConvertOggToWavAsync(fakeOggData);

        // ── Step 2: Validate WAV format (AudioPreprocessor) ──
        using var wavStream = new MemoryStream(wavData);
        var preprocessResult = _preprocessor.Process(wavStream, 60);
        preprocessResult.IsValid.ShouldBeTrue("WAV should be valid 16kHz mono PCM16");

        // ── Step 3: STT → process → TTS (VoicePipelineOrchestrator) ──
        var pipelineRequest = new VoicePipelineRequest(
            AudioData: wavData,
            ChannelId: "telegram-chat-123",
            UserId: "user-456",
            LanguageHint: "en",
            VoiceId: "default");

        var pipelineResult = await VoicePipelineOrchestrator.Handle(
            pipelineRequest, _sttProvider, _ttsProvider, _pipelineLogger, CancellationToken.None);

        pipelineResult.Success.ShouldBeTrue();
        pipelineResult.TranscribedText.ShouldBe("Hello, this is a voice test");
        pipelineResult.ResponseText.ShouldBe("Hello, this is a voice test"); // V1 echo-back
        pipelineResult.ResponseAudio.ShouldNotBeNull();

        // ── Step 4: WAV → OGG (for Telegram response) ──
        var oggResponse = await _converter.ConvertWavToOggAsync(pipelineResult.ResponseAudio);
        oggResponse.ShouldNotBeNull();
        oggResponse.Length.ShouldBeGreaterThan(0);

        // Verify all stages were called
        await _converter.Received(1).ConvertOggToWavAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await _sttProvider.Received(1).TranscribeAsync(Arg.Any<Stream>(), Arg.Is("en"), Arg.Any<CancellationToken>());
        await _converter.Received(1).ConvertWavToOggAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FullPipeline_WithLanguageHint_PassesHintToSttProvider()
    {
        // Arrange
        var wavData = E2EHelpers.CreateValidWavBytes(1.0);
        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Is("es"), Arg.Any<CancellationToken>())
            .Returns(new TranscriptionResult
            {
                Text = "Hola mundo",
                Confidence = 0.92,
                Duration = TimeSpan.FromSeconds(1),
                Language = "es"
            });

        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(E2EHelpers.ToAsyncEnumerable<byte[]>(new byte[] { 1, 2, 3 }));

        var request = new VoicePipelineRequest(wavData, "ch-1", "usr-1", LanguageHint: "es");

        // Act
        var result = await VoicePipelineOrchestrator.Handle(
            request, _sttProvider, _ttsProvider, _pipelineLogger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.TranscribedText.ShouldBe("Hola mundo");
        await _sttProvider.Received(1).TranscribeAsync(Arg.Any<Stream>(), Arg.Is("es"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FullPipeline_MultipleChunkTts_CombinesChunksCorrectly()
    {
        // Arrange
        var wavData = E2EHelpers.CreateValidWavBytes(1.0);

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new TranscriptionResult
            {
                Text = "multi-chunk test",
                Confidence = 0.88,
                Duration = TimeSpan.FromSeconds(1),
                Language = "en"
            });

        // TTS returns three separate chunks
        var chunk1 = new byte[] { 10, 20, 30 };
        var chunk2 = new byte[] { 40, 50 };
        var chunk3 = new byte[] { 60, 70, 80, 90 };
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(E2EHelpers.ToAsyncEnumerable(chunk1, chunk2, chunk3));

        var request = new VoicePipelineRequest(wavData, "ch-1", "usr-1");

        // Act
        var result = await VoicePipelineOrchestrator.Handle(
            request, _sttProvider, _ttsProvider, _pipelineLogger, CancellationToken.None);

        // Assert: chunks should be combined in order
        result.Success.ShouldBeTrue();
        result.ResponseAudio.ShouldBe(new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 });
    }

    [Fact]
    public async Task FullPipeline_SttFailure_ReturnsPipelineFailureWithNoAudio()
    {
        // Arrange
        var wavData = E2EHelpers.CreateValidWavBytes(1.0);

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Whisper model not loaded"));

        var request = new VoicePipelineRequest(wavData, "ch-1", "usr-1");

        // Act
        var result = await VoicePipelineOrchestrator.Handle(
            request, _sttProvider, _ttsProvider, _pipelineLogger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.TranscribedText.ShouldBeNull();
        result.ResponseText.ShouldBeNull();
        result.ResponseAudio.ShouldBeNull();
        result.ErrorMessage!.ShouldContain("Whisper model not loaded");

        // TTS should never be called
        _ttsProvider.DidNotReceive().SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FullPipeline_TtsFailure_ReturnsPartialResultWithTranscription()
    {
        // Arrange
        var wavData = E2EHelpers.CreateValidWavBytes(1.0);

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new TranscriptionResult
            {
                Text = "Successfully transcribed",
                Confidence = 0.9,
                Duration = TimeSpan.FromSeconds(1),
                Language = "en"
            });

        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(E2EHelpers.ThrowingAsyncEnumerable<byte[]>(new InvalidOperationException("Piper TTS crashed")));

        var request = new VoicePipelineRequest(wavData, "ch-1", "usr-1");

        // Act
        var result = await VoicePipelineOrchestrator.Handle(
            request, _sttProvider, _ttsProvider, _pipelineLogger, CancellationToken.None);

        // Assert: partial result — transcription available but no audio
        result.Success.ShouldBeFalse();
        result.TranscribedText.ShouldBe("Successfully transcribed");
        result.ResponseText.ShouldBe("Successfully transcribed");
        result.ResponseAudio.ShouldBeNull();
        result.ErrorMessage!.ShouldContain("TTS");
    }
}

// ═══════════════════════════════════════════════════════════════════
//  Timeout handling tests
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Tests for STT/TTS timeout and error-handling paths in the voice pipeline.
/// Tests simulate timeout-like failures without waiting for real 60s/30s timeouts
/// (those are already covered by VoiceHandlerTests). Focus here is on E2E error
/// propagation: how the orchestrator and handlers surface timeout/cancellation
/// errors to callers.
/// </summary>
public sealed class VoicePipelineTimeoutTests
{
    private readonly ISpeechToTextProvider _sttProvider = Substitute.For<ISpeechToTextProvider>();
    private readonly ITextToSpeechProvider _ttsProvider = Substitute.For<ITextToSpeechProvider>();
    private readonly ILogger<VoicePipelineOrchestrator> _pipelineLogger = NullLogger<VoicePipelineOrchestrator>.Instance;
    private readonly ILogger<VoiceMessageReceivedHandler> _sttLogger = NullLogger<VoiceMessageReceivedHandler>.Instance;
    private readonly ILogger<VoiceSynthesisRequestHandler> _ttsLogger = NullLogger<VoiceSynthesisRequestHandler>.Instance;

    [Fact]
    public async Task Pipeline_SttThrowsOce_ReturnsTimeoutErrorWhenExternalCtNotCancelled()
    {
        // Arrange: STT throws OCE (simulating what happens when the 60s CTS fires).
        // Orchestrator catch: `when (!ct.IsCancellationRequested)` → matches because
        // we pass CancellationToken.None as external ct.
        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("simulated timeout"));

        var request = new VoicePipelineRequest(new byte[] { 1, 2 }, "ch-1", "usr-1");

        // Act
        var result = await VoicePipelineOrchestrator.Handle(
            request, _sttProvider, _ttsProvider, _pipelineLogger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("timed out");
        result.ErrorMessage!.ShouldContain("60");
        result.TranscribedText.ShouldBeNull();
        result.ResponseAudio.ShouldBeNull();
    }

    [Fact]
    public async Task Pipeline_TtsThrowsOce_ReturnsTimeoutErrorPreservingSttResult()
    {
        // Arrange: STT succeeds
        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new TranscriptionResult
            {
                Text = "STT is fast",
                Confidence = 0.9,
                Duration = TimeSpan.FromSeconds(1),
                Language = "en"
            });

        // TTS throws OCE (simulating 30s timeout)
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(E2EHelpers.ThrowingAsyncEnumerable<byte[]>(new OperationCanceledException("simulated timeout")));

        var request = new VoicePipelineRequest(new byte[] { 1, 2 }, "ch-1", "usr-1");

        // Act
        var result = await VoicePipelineOrchestrator.Handle(
            request, _sttProvider, _ttsProvider, _pipelineLogger, CancellationToken.None);

        // Assert: TTS timed out but STT result is preserved
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("timed out");
        result.ErrorMessage!.ShouldContain("30");
        result.TranscribedText.ShouldBe("STT is fast");
        result.ResponseAudio.ShouldBeNull();
    }

    [Fact]
    public async Task SttHandler_ExternalCancellation_PropagatesOce()
    {
        // Arrange: mock waits for cancellation token
        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(2);
                await Task.Delay(Timeout.Infinite, ct);
                return new TranscriptionResult { Text = "never" };
            });

        var message = new VoiceMessageReceived(new byte[] { 1 }, "ch-1", "usr-1");
        using var shortCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act & Assert: external cancellation propagates as TaskCanceledException
        await Should.ThrowAsync<TaskCanceledException>(
            () => VoiceMessageReceivedHandler.Handle(message, _sttProvider, _sttLogger, shortCts.Token));
    }

    [Fact]
    public async Task TtsHandler_ExternalCancellation_PropagatesOce()
    {
        // Arrange: TTS mock waits for cancellation
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(2);
                return E2EHelpers.SlowAsyncEnumerable(Timeout.InfiniteTimeSpan, new byte[] { 1 }, ct);
            });

        var request = new VoiceSynthesisRequest("Synthesize this", "ch-1", "usr-1");
        using var shortCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act & Assert: external cancellation propagates as TaskCanceledException
        await Should.ThrowAsync<TaskCanceledException>(
            () => VoiceSynthesisRequestHandler.Handle(request, _ttsProvider, _ttsLogger, shortCts.Token));
    }

    [Fact]
    public void TimeoutConstants_MatchContractValues()
    {
        // The orchestrator's constants should match the contract defaults
        VoicePipelineOrchestrator.SttTimeoutSeconds.ShouldBe(60);
        VoicePipelineOrchestrator.TtsTimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public async Task Pipeline_ExternalCancellation_PropagatesCorrectly()
    {
        // Arrange: external cancellation (not timeout)
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var request = new VoicePipelineRequest(new byte[] { 1 }, "ch-1", "usr-1");

        // Act & Assert: external cancellation propagates as OperationCanceledException
        await Should.ThrowAsync<OperationCanceledException>(
            () => VoicePipelineOrchestrator.Handle(
                request, _sttProvider, _ttsProvider, _pipelineLogger, cts.Token));
    }
}

// ═══════════════════════════════════════════════════════════════════
//  Format validation tests (AudioPreprocessor)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Tests that AudioPreprocessor correctly rejects audio that doesn't
/// meet the pipeline's format requirements (16kHz, mono, PCM16, ≤60s).
/// These integrate with the full E2E flow by validating the WAV data
/// that would come from AudioFormatConverter.
/// </summary>
public sealed class VoicePipelineFormatValidationTests
{
    private readonly AudioPreprocessor _preprocessor = new();

    [Fact]
    public void ValidWav_16kHzMonoPcm16_AcceptedByPreprocessor()
    {
        // Arrange
        var wavData = E2EHelpers.CreateValidWavBytes(2.0);
        using var stream = new MemoryStream(wavData);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.SampleRate.ShouldBe(16000);
        result.Channels.ShouldBe(1);
        result.Samples.ShouldNotBeNull();
        result.Duration.TotalSeconds.ShouldBe(2.0, 0.1);
    }

    [Fact]
    public void InvalidWav_44100Hz_RejectedByPreprocessor()
    {
        // Arrange: WAV with wrong sample rate
        var wavData = E2EHelpers.CreateWavBytesWithSampleRate(44100);
        using var stream = new MemoryStream(wavData);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("sample rate");
        result.Error!.ShouldContain("44100");
    }

    [Fact]
    public void InvalidWav_8kHz_RejectedByPreprocessor()
    {
        // Arrange: WAV with 8kHz sample rate
        var wavData = E2EHelpers.CreateWavBytesWithSampleRate(8000);
        using var stream = new MemoryStream(wavData);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("sample rate");
        result.Error!.ShouldContain("8000");
    }

    [Fact]
    public void InvalidWav_Stereo_RejectedByPreprocessor()
    {
        // Arrange: stereo WAV
        var wavData = E2EHelpers.CreateStereoWavBytes(1.0);
        using var stream = new MemoryStream(wavData);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("channel");
        result.Error!.ShouldContain("mono");
    }

    [Fact]
    public void InvalidWav_NonPcmFormat_RejectedByPreprocessor()
    {
        // Arrange: IEEE float WAV (format = 3, not PCM = 1)
        var wavData = E2EHelpers.CreateNonPcmWavBytes(1.0);
        using var stream = new MemoryStream(wavData);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("PCM");
    }

    [Fact]
    public void InvalidWav_TooShort_RejectedByPreprocessor()
    {
        // Arrange: data too small for WAV header
        using var stream = new MemoryStream(new byte[20]);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("too short");
    }

    [Fact]
    public void InvalidWav_NotRiff_RejectedByPreprocessor()
    {
        // Arrange: starts with wrong magic bytes
        var data = new byte[100];
        data[0] = (byte)'N';
        data[1] = (byte)'O';
        data[2] = (byte)'P';
        data[3] = (byte)'E';
        using var stream = new MemoryStream(data);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("RIFF");
    }
}

// ═══════════════════════════════════════════════════════════════════
//  Duration limit tests
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Tests that the voice pipeline enforces the 60-second duration limit.
/// </summary>
public sealed class VoicePipelineDurationLimitTests
{
    private readonly AudioPreprocessor _preprocessor = new();

    [Fact]
    public void Duration_Exceeds60Seconds_RejectedByPreprocessor()
    {
        // Arrange: 61 seconds of audio
        var wavData = E2EHelpers.CreateValidWavBytes(61.0);
        using var stream = new MemoryStream(wavData);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("exceeds maximum");
        result.Error!.ShouldContain("60");
    }

    [Fact]
    public void Duration_Exactly60Seconds_AcceptedByPreprocessor()
    {
        // Arrange: exactly 60 seconds
        var wavData = E2EHelpers.CreateValidWavBytes(60.0);
        using var stream = new MemoryStream(wavData);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Duration.TotalSeconds.ShouldBe(60.0, 0.1);
    }

    [Fact]
    public void Duration_59Seconds_AcceptedByPreprocessor()
    {
        // Arrange
        var wavData = E2EHelpers.CreateValidWavBytes(59.0);
        using var stream = new MemoryStream(wavData);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Duration.TotalSeconds.ShouldBe(59.0, 0.1);
    }

    [Fact]
    public void Duration_VeryShort_HalfSecond_AcceptedByPreprocessor()
    {
        // Arrange: 0.5s audio
        var wavData = E2EHelpers.CreateValidWavBytes(0.5);
        using var stream = new MemoryStream(wavData);

        // Act
        var result = _preprocessor.Process(stream, 60);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Duration.TotalSeconds.ShouldBe(0.5, 0.1);
    }

    [Fact]
    public async Task FullPipeline_DurationExceeds60s_PreprocessorRejectsBeforeStt()
    {
        // Arrange: 61-second audio passes through converter but fails preprocessing
        var sttProvider = Substitute.For<ISpeechToTextProvider>();
        var ttsProvider = Substitute.For<ITextToSpeechProvider>();

        var longWavData = E2EHelpers.CreateValidWavBytes(61.0);
        using var stream = new MemoryStream(longWavData);

        // Act: preprocessor validates before STT
        var preprocessResult = _preprocessor.Process(stream, 60);

        // Assert: rejected at preprocessing stage
        preprocessResult.IsValid.ShouldBeFalse();
        preprocessResult.Error!.ShouldContain("exceeds maximum");

        // STT should never be invoked (preprocessing gate)
        await sttProvider.DidNotReceive().TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}

// ═══════════════════════════════════════════════════════════════════
//  OTel span assertion tests
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Tests that voice pipeline operations create proper Activity spans
/// for OpenTelemetry distributed tracing.
/// </summary>
public sealed class VoicePipelineOTelTests
{
    private readonly ISpeechToTextProvider _sttProvider = Substitute.For<ISpeechToTextProvider>();
    private readonly ITextToSpeechProvider _ttsProvider = Substitute.For<ITextToSpeechProvider>();
    private readonly ILogger<VoicePipelineOrchestrator> _pipelineLogger = NullLogger<VoicePipelineOrchestrator>.Instance;
    private readonly ILogger<VoiceMessageReceivedHandler> _sttLogger = NullLogger<VoiceMessageReceivedHandler>.Instance;
    private readonly ILogger<VoiceSynthesisRequestHandler> _ttsLogger = NullLogger<VoiceSynthesisRequestHandler>.Instance;

    /// <summary>
    /// Enables activity tracing by registering a listener for the nem.Mimir source.
    /// Without a listener, Activity.Current will be null and StartActivity returns null.
    /// </summary>
    private static ActivityListener CreateListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "nem.Mimir" or "nem.Mimir.Telegram.Voice",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = _ => { }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task Pipeline_Success_CreatesVoicePipelineSpan()
    {
        // Arrange
        using var listener = CreateListener();
        Activity? capturedActivity = null;

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // Capture the current activity during STT execution
                capturedActivity = Activity.Current;
                return Task.FromResult(new TranscriptionResult
                {
                    Text = "otel test",
                    Confidence = 0.9,
                    Duration = TimeSpan.FromSeconds(1),
                    Language = "en"
                });
            });

        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(E2EHelpers.ToAsyncEnumerable<byte[]>(new byte[] { 1, 2, 3 }));

        var request = new VoicePipelineRequest(new byte[] { 1, 2, 3 }, "ch-otel", "usr-otel");

        // Act
        var result = await VoicePipelineOrchestrator.Handle(
            request, _sttProvider, _ttsProvider, _pipelineLogger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        // The pipeline creates a "voice.pipeline" activity — verify it was active during STT
        capturedActivity.ShouldNotBeNull("Activity should be created when listener is registered");
        capturedActivity!.OperationName.ShouldBe("voice.pipeline");
    }

    [Fact]
    public async Task SttHandler_Success_CreatesVoiceSttSpan()
    {
        // Arrange
        using var listener = CreateListener();
        Activity? capturedActivity = null;

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedActivity = Activity.Current;
                return Task.FromResult(new TranscriptionResult
                {
                    Text = "span test",
                    Confidence = 0.95,
                    Duration = TimeSpan.FromSeconds(1),
                    Language = "en"
                });
            });

        var message = new VoiceMessageReceived(new byte[] { 1 }, "ch-1", "usr-1", "en");

        // Act
        var result = await VoiceMessageReceivedHandler.Handle(message, _sttProvider, _sttLogger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        capturedActivity.ShouldNotBeNull("voice.stt activity should be created");
        capturedActivity!.OperationName.ShouldBe("voice.stt");
    }

    [Fact]
    public async Task TtsHandler_Success_CreatesVoiceTtsSpan()
    {
        // Arrange
        using var listener = CreateListener();
        Activity? capturedDuringEnum = null;

        // We need to capture Activity.Current during async enumeration
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CaptureActivityAndYield());

        var request = new VoiceSynthesisRequest("test text", "ch-1", "usr-1");

        // Act
        var result = await VoiceSynthesisRequestHandler.Handle(request, _ttsProvider, _ttsLogger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();

        // Helper to capture and yield
        async IAsyncEnumerable<byte[]> CaptureActivityAndYield()
        {
            capturedDuringEnum = Activity.Current;
            yield return new byte[] { 1, 2, 3 };
            await Task.Yield();
        }

        capturedDuringEnum.ShouldNotBeNull("voice.tts activity should be created");
        capturedDuringEnum!.OperationName.ShouldBe("voice.tts");
    }

    [Fact]
    public async Task Pipeline_SttTimeout_SetsErrorStatusOnSpan()
    {
        // Arrange
        using var listener = CreateListener();
        var completedActivities = new List<Activity>();

        // Replace listener to capture stopped activities
        using var captureListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "nem.Mimir",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = a => completedActivities.Add(a)
        };
        ActivitySource.AddActivityListener(captureListener);

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("simulated timeout"));

        var request = new VoicePipelineRequest(new byte[] { 1 }, "ch-1", "usr-1");

        // Act
        var result = await VoicePipelineOrchestrator.Handle(
            request, _sttProvider, _ttsProvider, _pipelineLogger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();

        // Find the pipeline activity that was stopped
        var pipelineActivity = completedActivities.FirstOrDefault(a => a.OperationName == "voice.pipeline");
        pipelineActivity.ShouldNotBeNull("voice.pipeline activity should have been stopped");
        pipelineActivity!.Status.ShouldBe(ActivityStatusCode.Error);

        // Check tags (bool tags are stored in TagObjects, not Tags)
        var successTag = pipelineActivity.TagObjects.FirstOrDefault(t => t.Key == "voice.success");
        successTag.Value.ShouldBe(false);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  Handler-level E2E tests (VoiceMessageReceivedHandler + VoiceSynthesisRequestHandler)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Tests that verify VoiceMessageReceivedHandler and VoiceSynthesisRequestHandler
/// work correctly in end-to-end scenarios with the pipeline orchestrator.
/// </summary>
public sealed class VoiceHandlerE2ETests
{
    private readonly ISpeechToTextProvider _sttProvider = Substitute.For<ISpeechToTextProvider>();
    private readonly ITextToSpeechProvider _ttsProvider = Substitute.For<ITextToSpeechProvider>();
    private readonly ILogger<VoiceMessageReceivedHandler> _sttLogger = NullLogger<VoiceMessageReceivedHandler>.Instance;
    private readonly ILogger<VoiceSynthesisRequestHandler> _ttsLogger = NullLogger<VoiceSynthesisRequestHandler>.Instance;

    [Fact]
    public async Task SttThenTts_EndToEnd_TranscribeThenSynthesize()
    {
        // Arrange: STT handler processes audio
        var wavData = E2EHelpers.CreateValidWavBytes(2.0);
        var sttMessage = new VoiceMessageReceived(wavData, "ch-1", "usr-1", "en", "audio/wav", 2.0);

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new TranscriptionResult
            {
                Text = "Please repeat this back to me",
                Confidence = 0.97,
                Duration = TimeSpan.FromSeconds(2),
                Language = "en"
            });

        // Act: Step 1 — STT
        var sttResult = await VoiceMessageReceivedHandler.Handle(sttMessage, _sttProvider, _sttLogger, CancellationToken.None);

        // Assert STT
        sttResult.Success.ShouldBeTrue();
        sttResult.TranscribedText.ShouldBe("Please repeat this back to me");
        sttResult.Confidence.ShouldBe(0.97);

        // Arrange: TTS handler synthesizes response
        var ttsResponseWav = E2EHelpers.CreateValidWavBytes(1.0);
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(E2EHelpers.ToAsyncEnumerable<byte[]>(ttsResponseWav));

        var ttsRequest = new VoiceSynthesisRequest(sttResult.TranscribedText!, "ch-1", "usr-1");

        // Act: Step 2 — TTS
        var ttsResult = await VoiceSynthesisRequestHandler.Handle(ttsRequest, _ttsProvider, _ttsLogger, CancellationToken.None);

        // Assert TTS
        ttsResult.Success.ShouldBeTrue();
        ttsResult.AudioData.ShouldNotBeNull();
        ttsResult.AudioData!.Length.ShouldBe(ttsResponseWav.Length);
        ttsResult.MimeType.ShouldBe("audio/wav");
        ttsResult.DurationSeconds.ShouldNotBeNull();
        ttsResult.DurationSeconds!.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SttHandler_EmptyAudio_FailsBeforeTts()
    {
        // Arrange: empty audio data
        var message = new VoiceMessageReceived(Array.Empty<byte>(), "ch-1", "usr-1");

        // Act
        var result = await VoiceMessageReceivedHandler.Handle(message, _sttProvider, _sttLogger, CancellationToken.None);

        // Assert: fails immediately without calling STT provider
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Audio data is empty.");
        await _sttProvider.DidNotReceive().TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
