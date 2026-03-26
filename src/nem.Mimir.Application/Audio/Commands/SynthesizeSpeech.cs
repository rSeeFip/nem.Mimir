using FluentValidation;
using nem.Contracts.Voice;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Audio.Commands;

public sealed record SynthesizeSpeechCommand(string Text, string? Voice, double Speed) : ICommand<byte[]>;

public sealed class SynthesizeSpeechCommandValidator : AbstractValidator<SynthesizeSpeechCommand>
{
    public SynthesizeSpeechCommandValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Text is required.")
            .MaximumLength(5000).WithMessage("Text must not exceed 5000 characters.");

        RuleFor(x => x.Voice)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Voice))
            .WithMessage("Voice identifier is too long.");

        RuleFor(x => x.Speed)
            .InclusiveBetween(0.25, 4.0)
            .WithMessage("Speed must be between 0.25 and 4.0.");
    }
}

public sealed class SynthesizeSpeechHandler
{
    public async Task<byte[]> Handle(
        SynthesizeSpeechCommand command,
        ITextToSpeechProvider ttsProvider,
        CancellationToken cancellationToken)
    {
        var normalizedText = command.Text.Trim();
        var audio = await ttsProvider
            .SynthesizeFullAsync(normalizedText, command.Voice, cancellationToken)
            .ConfigureAwait(false);

        return audio;
    }
}
