using nem.Contracts.Voice;
using nem.Mimir.Application.Audio.Commands;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Audio;

public sealed class TranscribeAudioTests
{
    [Fact]
    public async Task Handle_ValidAudio_Should_Return_TranscriptionDto()
    {
        var provider = Substitute.For<ISpeechToTextProvider>();
        var handler = new TranscribeAudioHandler();

        provider
            .TranscribeAsync(Arg.Any<Stream>(), "en", Arg.Any<CancellationToken>())
            .Returns(new TranscriptionResult
            {
                Text = "hello world",
                Confidence = 0.92,
                Duration = TimeSpan.FromSeconds(2.5),
                Language = "en",
            });

        var command = new TranscribeAudioCommand([0x01, 0x02, 0x03], "en");

        var result = await handler.Handle(command, provider, CancellationToken.None);

        result.Text.ShouldBe("hello world");
        result.Confidence.ShouldBe(0.92);
        result.DurationSeconds.ShouldBe(2.5);
        result.Language.ShouldBe("en");

        await provider.Received(1)
            .TranscribeAsync(Arg.Any<Stream>(), "en", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoLanguageHint_Should_Forward_Null_Language()
    {
        var provider = Substitute.For<ISpeechToTextProvider>();
        var handler = new TranscribeAudioHandler();

        provider
            .TranscribeAsync(Arg.Any<Stream>(), null, Arg.Any<CancellationToken>())
            .Returns(new TranscriptionResult
            {
                Text = "hola",
                Confidence = 0.88,
                Duration = TimeSpan.FromSeconds(1.2),
                Language = "es",
            });

        var command = new TranscribeAudioCommand([0x0A], null);

        var result = await handler.Handle(command, provider, CancellationToken.None);

        result.Text.ShouldBe("hola");
        result.Language.ShouldBe("es");

        await provider.Received(1)
            .TranscribeAsync(Arg.Any<Stream>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyAudio_Should_Fail()
    {
        var validator = new TranscribeAudioCommandValidator();
        var command = new TranscribeAudioCommand([], "en");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == "AudioData");
    }
}
