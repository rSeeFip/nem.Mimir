using FluentValidation;
using nem.Contracts.Voice;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Audio.Queries;

public sealed record GetVoiceOptionsQuery : IQuery<VoiceOptionsDto>;

public sealed record VoiceOptionsDto(
    IReadOnlyList<string> AvailableVoices,
    double MinSpeed,
    double MaxSpeed,
    string DefaultVoice,
    int DefaultSampleRate,
    int DefaultChannels,
    string DefaultFormat,
    int MaxDurationSeconds,
    int TimeoutSeconds);

public sealed class GetVoiceOptionsQueryValidator : AbstractValidator<GetVoiceOptionsQuery>
{
}

public sealed class GetVoiceOptionsHandler
{
    public Task<VoiceOptionsDto> Handle(GetVoiceOptionsQuery query, CancellationToken cancellationToken)
    {
        var options = new VoicePipelineOptions();

        var result = new VoiceOptionsDto(
            AvailableVoices: ["default", "alloy", "nova", "echo"],
            MinSpeed: 0.25,
            MaxSpeed: 4.0,
            DefaultVoice: "default",
            DefaultSampleRate: options.SampleRate,
            DefaultChannels: options.Channels,
            DefaultFormat: options.Format,
            MaxDurationSeconds: (int)options.MaxDuration.TotalSeconds,
            TimeoutSeconds: (int)options.Timeout.TotalSeconds);

        return Task.FromResult(result);
    }
}
