using FluentValidation;
using nem.Contracts.Voice;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Audio.Commands;

public sealed record TranscribeAudioCommand(byte[] AudioData, string? LanguageHint) : ICommand<TranscriptionDto>;

public sealed record TranscriptionDto(string Text, double Confidence, double DurationSeconds, string? Language);

public sealed class TranscribeAudioCommandValidator : AbstractValidator<TranscribeAudioCommand>
{
    public TranscribeAudioCommandValidator()
    {
        RuleFor(x => x.AudioData)
            .NotEmpty().WithMessage("Audio data is required.")
            .Must(data => data.Length > 0).WithMessage("Audio data is required.");

        RuleFor(x => x.AudioData)
            .Must(data => data.Length <= 50 * 1024 * 1024)
            .WithMessage("Audio file must be 50MB or smaller.");

        RuleFor(x => x.LanguageHint)
            .MaximumLength(10)
            .When(x => !string.IsNullOrWhiteSpace(x.LanguageHint))
            .WithMessage("Language hint is too long.");
    }
}

public sealed class TranscribeAudioHandler
{
    public async Task<TranscriptionDto> Handle(
        TranscribeAudioCommand command,
        ISpeechToTextProvider sttProvider,
        CancellationToken cancellationToken)
    {
        using var audioStream = new MemoryStream(command.AudioData);
        var transcription = await sttProvider
            .TranscribeAsync(audioStream, command.LanguageHint, cancellationToken)
            .ConfigureAwait(false);

        return new TranscriptionDto(
            transcription.Text,
            transcription.Confidence,
            transcription.Duration.TotalSeconds,
            transcription.Language);
    }
}
