using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Voice;
using nem.Mimir.Voice.Configuration;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Voice.Tests;

/// <summary>
/// Tests for PiperTtsBackend. Since PiperTtsBackend wraps a Process (piper binary),
/// we test through the ITtsBackend interface with mocking at a higher level.
/// Direct PiperTtsBackend tests verify construction and configuration usage.
/// </summary>
public class PiperTtsBackendTests
{
    [Fact]
    public void PiperTtsBackend_CanBeConstructed()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PiperTtsBackend>>();
        var options = Substitute.For<IOptions<TtsOptions>>();
        options.Value.Returns(new TtsOptions
        {
            ModelPath = "/test/model.onnx",
            DefaultVoiceId = "test-voice"
        });

        // Act
        var backend = new PiperTtsBackend(logger, options);

        // Assert
        backend.ShouldNotBeNull();
    }

    [Fact]
    public async Task GenerateAudioAsync_WhenPiperNotInstalled_ThrowsException()
    {
        // Arrange
        var logger = Substitute.For<ILogger<PiperTtsBackend>>();
        var options = Substitute.For<IOptions<TtsOptions>>();
        options.Value.Returns(new TtsOptions
        {
            ModelPath = "/nonexistent/model.onnx"
        });

        var backend = new PiperTtsBackend(logger, options);

        // Act & Assert — piper binary won't exist in test env
        await Should.ThrowAsync<Exception>(
            () => backend.GenerateAudioAsync("test", "/nonexistent/model.onnx", CancellationToken.None));
    }

    [Fact]
    public async Task ITtsBackend_CanBeMocked_WithNSubstitute()
    {
        // Arrange
        var mockBackend = Substitute.For<ITtsBackend>();
        var expectedAudio = new byte[] { 0x01, 0x02, 0x03 };

        mockBackend.GenerateAudioAsync("hello", "model.onnx", Arg.Any<CancellationToken>())
            .Returns(expectedAudio);

        // Act
        var result = await mockBackend.GenerateAudioAsync("hello", "model.onnx", CancellationToken.None);

        // Assert
        result.ShouldBe(expectedAudio);
    }

    [Fact]
    public async Task ITtsBackend_MockedCancellation_ThrowsOperationCanceled()
    {
        // Arrange
        var mockBackend = Substitute.For<ITtsBackend>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        mockBackend.GenerateAudioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<byte[]>(_ => throw new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => mockBackend.GenerateAudioAsync("test", "model", cts.Token));
    }
}
