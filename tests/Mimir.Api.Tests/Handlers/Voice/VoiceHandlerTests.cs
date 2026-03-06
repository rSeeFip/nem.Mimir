using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mimir.Api.Handlers.Voice;
using Mimir.Api.Handlers.Voice.Messages;
using nem.Contracts.Voice;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Mimir.Api.Tests.Handlers.Voice;

// ═══════════════════════════════════════════════════════════════════
//  Shared helpers
// ═══════════════════════════════════════════════════════════════════

internal static class AsyncEnumerableHelpers
{
    internal static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    internal static async IAsyncEnumerable<T> ThrowingAsyncEnumerable<T>(Exception ex)
    {
        await Task.Yield();
        throw ex;
#pragma warning disable CS0162 // Unreachable code detected
        yield break; // required to make this an async iterator
#pragma warning restore CS0162
    }
}

// ═══════════════════════════════════════════════════════════════════
//  VoiceMessageReceivedHandler tests (STT)
// ═══════════════════════════════════════════════════════════════════

public sealed class VoiceMessageReceivedHandlerTests
{
    private readonly ISpeechToTextProvider _sttProvider = Substitute.For<ISpeechToTextProvider>();
    private readonly ILogger<VoiceMessageReceivedHandler> _logger = NullLogger<VoiceMessageReceivedHandler>.Instance;

    [Fact]
    public async Task Handle_SuccessfulTranscription_ReturnsSuccessResult()
    {
        // Arrange
        var audioBytes = new byte[] { 1, 2, 3, 4 };
        var message = new VoiceMessageReceived(audioBytes, "ch-1", "usr-1", "en");

        var transcription = new TranscriptionResult
        {
            Text = "Hello world",
            Confidence = 0.95,
            Duration = TimeSpan.FromSeconds(2.5),
            Language = "en"
        };

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(transcription);

        // Act
        var result = await VoiceMessageReceivedHandler.Handle(message, _sttProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.TranscribedText.ShouldBe("Hello world");
        result.Confidence.ShouldBe(0.95);
        result.Language.ShouldBe("en");
        result.Duration.ShouldBe(TimeSpan.FromSeconds(2.5));
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_EmptyAudioData_ReturnsFailureResult()
    {
        // Arrange
        var message = new VoiceMessageReceived([], "ch-1", "usr-1");

        // Act
        var result = await VoiceMessageReceivedHandler.Handle(message, _sttProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.TranscribedText.ShouldBeNull();
        result.Confidence.ShouldBe(0);
        result.ErrorMessage.ShouldBe("Audio data is empty.");
        await _sttProvider.DidNotReceive().TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SttThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var message = new VoiceMessageReceived(new byte[] { 1, 2 }, "ch-1", "usr-1");

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Whisper unavailable"));

        // Act
        var result = await VoiceMessageReceivedHandler.Handle(message, _sttProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.TranscribedText.ShouldBeNull();
        result.ErrorMessage!.ShouldContain("Whisper unavailable");
    }

    [Fact]
    public async Task Handle_NullMessage_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => VoiceMessageReceivedHandler.Handle(null!, _sttProvider, _logger, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SttTimeout_ReturnsTimeoutError()
    {
        // Arrange
        var message = new VoiceMessageReceived(new byte[] { 1, 2 }, "ch-1", "usr-1");

        // Simulate a provider that stalls, then cancellation kicks in
        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(2);
                await Task.Delay(TimeSpan.FromSeconds(120), ct); // will be cancelled by 60s timeout
                return new TranscriptionResult { Text = "never" };
            });

        // Act
        var result = await VoiceMessageReceivedHandler.Handle(message, _sttProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("timed out");
    }

    [Fact]
    public async Task Handle_SuccessWithNullLanguage_ReturnsNullLanguage()
    {
        // Arrange
        var message = new VoiceMessageReceived(new byte[] { 1 }, "ch-1", "usr-1");

        var transcription = new TranscriptionResult
        {
            Text = "Hola",
            Confidence = 0.8,
            Duration = TimeSpan.FromSeconds(1),
            Language = null
        };

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(transcription);

        // Act
        var result = await VoiceMessageReceivedHandler.Handle(message, _sttProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.TranscribedText.ShouldBe("Hola");
        result.Language.ShouldBeNull();
    }
}

// ═══════════════════════════════════════════════════════════════════
//  VoiceSynthesisRequestHandler tests (TTS)
// ═══════════════════════════════════════════════════════════════════

public sealed class VoiceSynthesisRequestHandlerTests
{
    private readonly ITextToSpeechProvider _ttsProvider = Substitute.For<ITextToSpeechProvider>();
    private readonly ILogger<VoiceSynthesisRequestHandler> _logger = NullLogger<VoiceSynthesisRequestHandler>.Instance;

    private void SetupTtsReturns(params byte[][] chunks)
    {
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerableHelpers.ToAsyncEnumerable(chunks));
    }

    private void SetupTtsThrows(Exception ex)
    {
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerableHelpers.ThrowingAsyncEnumerable<byte[]>(ex));
    }

    [Fact]
    public async Task Handle_SuccessfulSynthesis_ReturnsAudioResult()
    {
        // Arrange
        var request = new VoiceSynthesisRequest("Hello world", "ch-1", "usr-1");
        SetupTtsReturns(new byte[] { 10, 20 }, new byte[] { 30, 40, 50 });

        // Act
        var result = await VoiceSynthesisRequestHandler.Handle(request, _ttsProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.AudioData.ShouldNotBeNull();
        result.AudioData.Length.ShouldBe(5); // 2 + 3
        result.AudioData.ShouldBe(new byte[] { 10, 20, 30, 40, 50 });
        result.MimeType.ShouldBe("audio/wav");
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_EmptyText_ThrowsArgumentException()
    {
        var request = new VoiceSynthesisRequest("", "ch-1", "usr-1");

        await Should.ThrowAsync<ArgumentException>(
            () => VoiceSynthesisRequestHandler.Handle(request, _ttsProvider, _logger, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhitespaceText_ThrowsArgumentException()
    {
        var request = new VoiceSynthesisRequest("   ", "ch-1", "usr-1");

        await Should.ThrowAsync<ArgumentException>(
            () => VoiceSynthesisRequestHandler.Handle(request, _ttsProvider, _logger, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => VoiceSynthesisRequestHandler.Handle(null!, _ttsProvider, _logger, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_TtsThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var request = new VoiceSynthesisRequest("Say something", "ch-1", "usr-1");
        SetupTtsThrows(new InvalidOperationException("TTS engine down"));

        // Act
        var result = await VoiceSynthesisRequestHandler.Handle(request, _ttsProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.AudioData.ShouldBeNull();
        result.ErrorMessage!.ShouldContain("TTS engine down");
    }

    [Fact]
    public async Task Handle_SingleChunk_ReturnsCombinedAudio()
    {
        // Arrange
        var request = new VoiceSynthesisRequest("Hi", "ch-1", "usr-1");
        SetupTtsReturns(new byte[] { 1, 2, 3 });

        // Act
        var result = await VoiceSynthesisRequestHandler.Handle(request, _ttsProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.AudioData.ShouldBe(new byte[] { 1, 2, 3 });
    }
}

// ═══════════════════════════════════════════════════════════════════
//  VoicePipelineOrchestrator tests (full pipeline)
// ═══════════════════════════════════════════════════════════════════

public sealed class VoicePipelineOrchestratorTests
{
    private readonly ISpeechToTextProvider _sttProvider = Substitute.For<ISpeechToTextProvider>();
    private readonly ITextToSpeechProvider _ttsProvider = Substitute.For<ITextToSpeechProvider>();
    private readonly ILogger<VoicePipelineOrchestrator> _logger = NullLogger<VoicePipelineOrchestrator>.Instance;

    private void SetupSttReturns(TranscriptionResult result)
    {
        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    private void SetupTtsReturns(params byte[][] chunks)
    {
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerableHelpers.ToAsyncEnumerable(chunks));
    }

    private void SetupTtsThrows(Exception ex)
    {
        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerableHelpers.ThrowingAsyncEnumerable<byte[]>(ex));
    }

    [Fact]
    public async Task Handle_FullPipelineSuccess_ReturnsCompleteResult()
    {
        // Arrange
        var audioBytes = new byte[] { 1, 2, 3 };
        var request = new VoicePipelineRequest(audioBytes, "ch-1", "usr-1", "en", "voice-1");

        SetupSttReturns(new TranscriptionResult
        {
            Text = "Hello pipeline",
            Confidence = 0.9,
            Duration = TimeSpan.FromSeconds(3),
            Language = "en"
        });

        SetupTtsReturns(new byte[] { 10, 20, 30 });

        // Act
        var result = await VoicePipelineOrchestrator.Handle(request, _sttProvider, _ttsProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.TranscribedText.ShouldBe("Hello pipeline");
        result.ResponseText.ShouldBe("Hello pipeline"); // V1 echo-back
        result.ResponseAudio.ShouldNotBeNull();
        result.ResponseAudio.ShouldBe(new byte[] { 10, 20, 30 });
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_SttFailure_ReturnsFailureWithNullFields()
    {
        // Arrange
        var request = new VoicePipelineRequest(new byte[] { 1 }, "ch-1", "usr-1");

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("STT broke"));

        // Act
        var result = await VoicePipelineOrchestrator.Handle(request, _sttProvider, _ttsProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.TranscribedText.ShouldBeNull();
        result.ResponseText.ShouldBeNull();
        result.ResponseAudio.ShouldBeNull();
        result.ErrorMessage!.ShouldContain("STT");
    }

    [Fact]
    public async Task Handle_TtsFailure_ReturnsPartialResultWithTranscription()
    {
        // Arrange
        var request = new VoicePipelineRequest(new byte[] { 1 }, "ch-1", "usr-1");

        SetupSttReturns(new TranscriptionResult
        {
            Text = "Transcribed text",
            Confidence = 0.85,
            Duration = TimeSpan.FromSeconds(1),
            Language = "en"
        });

        SetupTtsThrows(new InvalidOperationException("TTS broke"));

        // Act
        var result = await VoicePipelineOrchestrator.Handle(request, _sttProvider, _ttsProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.TranscribedText.ShouldBe("Transcribed text");
        result.ResponseText.ShouldBe("Transcribed text"); // V1 echo-back
        result.ResponseAudio.ShouldBeNull();
        result.ErrorMessage!.ShouldContain("TTS");
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => VoicePipelineOrchestrator.Handle(null!, _sttProvider, _ttsProvider, _logger, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SttTimeout_ReturnsTimeoutError()
    {
        // Arrange
        var request = new VoicePipelineRequest(new byte[] { 1 }, "ch-1", "usr-1");

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(2);
                await Task.Delay(TimeSpan.FromSeconds(120), ct);
                return new TranscriptionResult { Text = "never" };
            });

        // Act
        var result = await VoicePipelineOrchestrator.Handle(request, _sttProvider, _ttsProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("timed out");
        result.TranscribedText.ShouldBeNull();
    }

    [Fact]
    public void TimeoutConstants_AreCorrect()
    {
        VoicePipelineOrchestrator.SttTimeoutSeconds.ShouldBe(60);
        VoicePipelineOrchestrator.TtsTimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public async Task Handle_SttPassesLanguageHint_ToProvider()
    {
        // Arrange
        var request = new VoicePipelineRequest(new byte[] { 1 }, "ch-1", "usr-1", LanguageHint: "es");

        _sttProvider.TranscribeAsync(Arg.Any<Stream>(), Arg.Is("es"), Arg.Any<CancellationToken>())
            .Returns(new TranscriptionResult
            {
                Text = "Hola",
                Confidence = 0.9,
                Duration = TimeSpan.FromSeconds(1),
                Language = "es"
            });

        _ttsProvider.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerableHelpers.ToAsyncEnumerable<byte[]>(new byte[] { 1 }));

        // Act
        var result = await VoicePipelineOrchestrator.Handle(request, _sttProvider, _ttsProvider, _logger, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        await _sttProvider.Received(1).TranscribeAsync(Arg.Any<Stream>(), Arg.Is("es"), Arg.Any<CancellationToken>());
    }
}
